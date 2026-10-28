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
        var category = clasificacion.Trim().ToUpperInvariant();
        var command = category switch
        {
            "APROVECHABLE" => 'A',
            "NO_APROVECHABLE" => 'N',
            _ => '\0'
        };
        if (command == '\0')
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
                Comando = category,
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
            if (cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(2)))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            serialPort.DiscardInBuffer();
            serialPort.Write(command.ToString());

            return Task.FromResult(new ArduinoResult
            {
                Exito = true,
                Comando = category,
                Mensaje = $"Comando {command} enviado a Arduino."
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or TimeoutException)
        {
            logger.LogWarning(exception, "No fue posible enviar la clasificación al puerto serial {Port}.", settings.Port);
            return Task.FromResult(new ArduinoResult
            {
                Exito = false,
                Comando = category,
                Mensaje = "No se pudo comunicar con Arduino. Revisa el puerto y el protocolo serial."
            });
        }
    }
}
