using System.IO.Ports;
using Ecobox.Configuration;
using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;
using Microsoft.Extensions.Options;

namespace Ecobox.Services;

public sealed class ArduinoService(IOptions<ArduinoSettings> options, ILogger<ArduinoService> logger) : IArduinoService
{
    private static readonly SemaphoreSlim SerialPortLock = new(1, 1);

    public static string? MapearComandoSerial(string? clasificacion)
    {
        if (string.IsNullOrWhiteSpace(clasificacion)) return null;

        var category = new string(clasificacion.Where(character => !char.IsWhiteSpace(character)).ToArray()).ToUpperInvariant();
        return category switch
        {
            "APROVECHABLE" or "A" => "A\n",
            "NO_APROVECHABLE" or "NOAPROVECHABLE" or "N" => "N\n",
            _ => null
        };
    }

    public async Task<ArduinoResult> EnviarClasificacionAsync(string clasificacion, CancellationToken cancellationToken = default)
    {
        var wireCommand = MapearComandoSerial(clasificacion);
        if (wireCommand is null)
        {
            logger.LogWarning("[ARDUINO] Clasificación serial no válida: {Category}.", clasificacion);
            return new ArduinoResult
            {
                Exito = false,
                Comando = "ERROR",
                Mensaje = "La clasificación serial no es válida."
            };
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Port))
        {
            logger.LogWarning("[ARDUINO] No se configuró el puerto serial.");
            return new ArduinoResult
            {
                Exito = false,
                Comando = clasificacion.Trim(),
                Mensaje = "Arduino no está conectado: configura Arduino:Port."
            };
        }

        if (settings.BaudRate <= 0)
        {
            logger.LogError("[ARDUINO] Velocidad serial inválida: {BaudRate}.", settings.BaudRate);
            return new ArduinoResult
            {
                Exito = false,
                Comando = clasificacion.Trim(),
                Mensaje = "La velocidad serial configurada para Arduino no es válida."
            };
        }

        await SerialPortLock.WaitAsync(cancellationToken);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("[ARDUINO] Intentando conectar con {Port} a {BaudRate} baudios.", settings.Port, settings.BaudRate);
            using var serialPort = new SerialPort(settings.Port, settings.BaudRate)
            {
                NewLine = "\n",
                ReadTimeout = 2000,
                WriteTimeout = 2000
            };
            serialPort.Open();
            // Opening an ESP32 serial port may reset it; allow its firmware to boot before writing.
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            serialPort.DiscardInBuffer();
            logger.LogInformation("[ARDUINO] Enviando: {Command} (LF).", wireCommand.TrimEnd('\n'));
            serialPort.Write(wireCommand);
            var category = wireCommand[0] == 'A' ? "APROVECHABLE" : "NO_APROVECHABLE";
            logger.LogInformation("[ARDUINO] Clasificación {Category} enviada correctamente.", category);

            return new ArduinoResult
            {
                Exito = true,
                Comando = category,
                Mensaje = $"Comando {wireCommand.TrimEnd('\n')} enviado a Arduino."
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            logger.LogError(exception, "[ARDUINO] Error al enviar clasificación por {Port}: {Message}", settings.Port, exception.Message);
            return new ArduinoResult
            {
                Exito = false,
                Comando = clasificacion.Trim(),
                Mensaje = "No se pudo comunicar con Arduino. Revisa el puerto y el protocolo serial."
            };
        }
        finally
        {
            SerialPortLock.Release();
        }
    }
}
