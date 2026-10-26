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
    IOptions<VisionSettings> visionSettings) : IClasificacionResiduoService
{
    public async Task<ClasificacionResiduoResult> ProcesarImagenAsync(Stream imagen, CancellationToken cancellationToken = default)
    {
        var vision = await visionService.ClasificarAsync(imagen, cancellationToken);
        if (!vision.Exito || string.IsNullOrWhiteSpace(vision.Categoria))
        {
            return new ClasificacionResiduoResult { Mensaje = vision.Mensaje };
        }

        if (!Enum.TryParse<CategoriaResiduo>(vision.Categoria, true, out var categoria) || !Enum.IsDefined(categoria))
        {
            return new ClasificacionResiduoResult { Mensaje = "El modelo devolvió una categoría no permitida." };
        }

        if (vision.Confianza is null || (double)vision.Confianza < visionSettings.Value.ConfidenceThreshold)
        {
            return new ClasificacionResiduoResult
            {
                Categoria = categoria,
                Confianza = vision.Confianza,
                Mensaje = "La confianza de clasificación está por debajo del umbral configurado. No se registró el residuo."
            };
        }

        await registroService.RegistrarAsync(categoria, cancellationToken: cancellationToken);
        var arduino = await arduinoService.EnviarClasificacionAsync(categoria.ToString(), cancellationToken);

        return new ClasificacionResiduoResult
        {
            Exito = true,
            RegistroGuardado = true,
            Categoria = categoria,
            Confianza = vision.Confianza,
            Mensaje = arduino.Exito
                ? "Clasificación registrada y enviada a Arduino."
                : $"Clasificación registrada. {arduino.Mensaje}"
        };
    }
}
