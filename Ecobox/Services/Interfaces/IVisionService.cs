using Ecobox.Services.Results;

namespace Ecobox.Services.Interfaces;

public interface IVisionService
{
    Task<VisionResult> ClasificarAsync(Stream imagen, CancellationToken cancellationToken = default);
}
