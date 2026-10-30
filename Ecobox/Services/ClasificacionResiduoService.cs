using Ecobox.Configuration;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;
using Microsoft.Extensions.Options;

namespace Ecobox.Services;

public sealed class ClasificacionResiduoService(
    IVisionService visionService,
    IRegistroResiduoService registroService,
    IArduinoService arduinoService,
    IOptions<VisionSettings> visionSettings,
    IDetectionGateService detectionGate,
    ILogger<ClasificacionResiduoService> logger) : IClasificacionResiduoService
{
    public async Task<ClasificacionResiduoResult> ProcesarImagenAsync(Stream imagen, CancellationToken cancellationToken = default)
    {
        VisionResult vision;
        try
        {
            vision = await visionService.ClasificarAsync(imagen, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            detectionGate.ResetCandidate();
            logger.LogWarning("CONFIRMACION_RECHAZADA: inferencia cancelada; se limpió la predicción pendiente.");
            throw;
        }
        catch (Exception exception)
        {
            detectionGate.ResetCandidate();
            logger.LogError(exception, "CONFIRMACION_RECHAZADA: error inesperado durante la inferencia; se limpió la predicción pendiente.");
            throw;
        }
        if (!vision.Exito || string.IsNullOrWhiteSpace(vision.Categoria))
        {
            detectionGate.ResetCandidate();
            var lowConfidence = vision.Mensaje.Contains("confianza", StringComparison.OrdinalIgnoreCase);
            logger.LogWarning("CONFIRMACION_RECHAZADA: motivo={Reason}; predicción={Prediction}; confianza={Confidence:P1}.",
                lowConfidence ? "confianza_insuficiente" : "error_de_inferencia", vision.Prediccion, vision.Confianza);
            return new ClasificacionResiduoResult
            {
                Estado = lowConfidence ? "baja_confianza" : "error",
                Razon = lowConfidence ? "confianza_insuficiente" : "error_de_inferencia",
                Confianza = vision.Confianza,
                Mensaje = vision.Mensaje
            };
        }

        if (!Enum.TryParse<CategoriaResiduo>(vision.Categoria, true, out var categoria) || !Enum.IsDefined(categoria))
        {
            logger.LogError("CONFIRMACION_RECHAZADA: el modelo devolvió una categoría no permitida ({Category}).", vision.Categoria);
            return new ClasificacionResiduoResult { Estado = "error", Razon = "categoria_no_permitida", Mensaje = "El modelo devolvió una categoría no permitida." };
        }

        if (vision.Confianza is null || (double)vision.Confianza < visionSettings.Value.ConfidenceThreshold)
        {
            detectionGate.ResetCandidate();
            logger.LogWarning("CONFIRMACION_RECHAZADA: confianza {Confidence:P1} menor al umbral {Threshold:P0}.", vision.Confianza, visionSettings.Value.ConfidenceThreshold);
            return new ClasificacionResiduoResult
            {
                Estado = "baja_confianza",
                Razon = "confianza_insuficiente",
                Categoria = categoria,
                Confianza = vision.Confianza,
                Mensaje = "La confianza de clasificación está por debajo del umbral configurado. No se registró el residuo."
            };
        }

        logger.LogDebug("Predicción {Prediction}; categoría candidata {Category}; confianza {Confidence:P1}; {Diagnostic}",
            vision.Prediccion, categoria, vision.Confianza.Value, vision.Diagnostico);
        var decision = detectionGate.Evaluate(categoria, vision.Confianza.Value, vision.RoiHash);
        if (!decision.Confirmed)
        {
            return new ClasificacionResiduoResult
            {
                Categoria = categoria,
                Confianza = vision.Confianza,
                Estado = decision.State,
                Razon = decision.Reason,
                Mensaje = decision.Message
            };
        }

        try
        {
            // Once confirmed, finish the local transaction even if the browser request disconnects.
            await registroService.RegistrarAsync(categoria, cancellationToken: CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            detectionGate.CancelPendingRegistration();
            logger.LogWarning("CONFIRMACION_RECHAZADA: el registro SQLite fue cancelado; se liberó la reserva de registro.");
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            detectionGate.CancelPendingRegistration();
            logger.LogError(exception, "No se pudo guardar el residuo confirmado {Category} en SQLite.", categoria);
            return new ClasificacionResiduoResult
            {
                Estado = "error_registro",
                Razon = "sqlite_error",
                Categoria = categoria,
                Confianza = vision.Confianza,
                Mensaje = "La clasificación fue estable, pero no se pudo guardar el registro. Se reintentará."
            };
        }

        detectionGate.MarkRegistered();
        logger.LogInformation("REGISTRO_SQLITE: categoría={Category}; la operación transaccional terminó correctamente.", categoria);

        ArduinoResult arduino;
        try
        {
            logger.LogInformation("ARDUINO: enviando categoría {Category} después del registro SQLite confirmado.", categoria);
            arduino = await arduinoService.EnviarClasificacionAsync(categoria.ToString(), CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "El residuo {Category} se guardó, pero falló el envío a Arduino.", categoria);
            arduino = new ArduinoResult { Exito = false, Mensaje = "No se pudo enviar el comando a Arduino." };
        }

        logger.LogInformation("Residuo {Category} registrado. Arduino={ArduinoStatus}; Mensaje={ArduinoMessage}",
            categoria, arduino.Exito ? "enviado" : "no disponible", arduino.Mensaje);

        return new ClasificacionResiduoResult
        {
            Exito = true,
            RegistroGuardado = true,
            Estado = "registrada",
            Categoria = categoria,
            Confianza = vision.Confianza,
            Mensaje = arduino.Exito
                ? "Clasificación registrada y enviada a Arduino."
                : $"Clasificación registrada. {arduino.Mensaje}"
        };
    }
}
