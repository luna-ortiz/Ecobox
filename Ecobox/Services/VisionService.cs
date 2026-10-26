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
            Mensaje = "Modelo no disponible. La imagen no se clasificó ni se registró."
        });
    }
}
