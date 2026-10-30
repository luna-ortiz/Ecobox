using Ecobox.Services.Results;

namespace Ecobox.Services.Interfaces;

public interface IVisionService
{
    bool IsReady { get; }
    string StatusMessage { get; }

    Task<VisionResult> ClasificarAsync(Stream imagen, CancellationToken cancellationToken = default);
}
