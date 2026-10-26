using Ecobox.Services.Results;

namespace Ecobox.Services.Interfaces;

public interface IClasificacionResiduoService
{
    Task<ClasificacionResiduoResult> ProcesarImagenAsync(Stream imagen, CancellationToken cancellationToken = default);
}
