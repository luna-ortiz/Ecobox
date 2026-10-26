using Ecobox.Services.Results;

namespace Ecobox.Services.Interfaces;

public interface IPresenceDetectionService
{
    PresenceDetectionResult GetCurrentState();

    Task<PresenceDetectionResult> DetectAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default);
}
