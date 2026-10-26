using System.IO.Ports;
using Ecobox.Configuration;
using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;
using Microsoft.Extensions.Options;

namespace Ecobox.Services;

public sealed class ArduinoService(IOptions<ArduinoSettings> options, ILogger<ArduinoService> logger) : IArduinoService
{
    public Task<ArduinoResult> EnviarClasificacionAsync(string clasificacion, CancellationToken cancellationToken = default)
    {
        var command = clasificacion.Trim().ToUpperInvariant();
        if (command is not "APROVECHABLE" and not "NO_APROVECHABLE")
        {
            return Task.FromResult(new ArduinoResult
            {
                Exito = false,
                Comando = "ERROR",
                Mensaje = "La clasificación serial no es válida."
            });
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Port))
        {
            return Task.FromResult(new ArduinoResult
            {
                Exito = false,
                Comando = command,
                Mensaje = "Arduino no está conectado: configura Arduino:Port cuando se defina el puerto."
            });
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var serialPort = new SerialPort(settings.Port, settings.BaudRate)
            {
                NewLine = "\n",
                ReadTimeout = 2000,
                WriteTimeout = 2000
            };
            serialPort.Open();
            serialPort.DiscardInBuffer();
            serialPort.WriteLine(command);
            var response = serialPort.ReadLine().Trim();

            return Task.FromResult(new ArduinoResult
            {
                Exito = response == "LISTO",
                Comando = command,
                Mensaje = response == "LISTO"
                    ? "Arduino confirmó la recepción."
                    : response == "ERROR"
                        ? "Arduino respondió ERROR."
                        : $"Respuesta serial inesperada: {response}"
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or TimeoutException)
        {
            logger.LogWarning(exception, "No fue posible enviar la clasificación al puerto serial {Port}.", settings.Port);
            return Task.FromResult(new ArduinoResult
            {
                Exito = false,
                Comando = command,
                Mensaje = "No se pudo comunicar con Arduino. Revisa el puerto y el protocolo serial."
            });
        }
    }
}
