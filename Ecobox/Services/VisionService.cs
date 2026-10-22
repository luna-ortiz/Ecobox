using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;

namespace Ecobox.Services;

public sealed class VisionService : IVisionService
{
    public Task<VisionResult> ClasificarAsync(Stream imagen, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagen);
        return Task.FromResult(new VisionResult
        {
            Exito = false,
            Mensaje = "El modelo de visión aún no está integrado. La imagen no fue clasificada ni registrada."
        });
    }
}
