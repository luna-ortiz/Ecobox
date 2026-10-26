namespace Ecobox.Services.Results;

public sealed class PresenceDetectionResult
{
    public bool ResiduoDetectado { get; init; }
    public string Mensaje { get; init; } = string.Empty;
}
