namespace Ecobox.ViewModels;

public sealed class CamaraViewModel
{
    public string EstadoDeteccion { get; init; } = "Esperando residuo...";
    public string UltimaClasificacion { get; init; } = "Sin clasificaciones";
    public string UltimaConfianza { get; init; } = "—";
    public int FrameIntervalMs { get; init; }
    public int StableFramesRequired { get; init; }
    public int DetectionCooldownMs { get; init; }
    public double PresenceChangeThreshold { get; init; }
    public double RoiSizeRatio { get; init; }
}
