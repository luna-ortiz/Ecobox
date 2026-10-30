using Ecobox.Services.Interfaces;
using Ecobox.Services.Results;

namespace Ecobox.Services;

public sealed class PresenceDetectionService : IPresenceDetectionService
{
    public PresenceDetectionResult GetCurrentState() => new()
    {
        ResiduoDetectado = false,
        Mensaje = "Calibrando el fondo de la cámara…"
    };

    public Task<PresenceDetectionResult> DetectAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetCurrentState());
}
