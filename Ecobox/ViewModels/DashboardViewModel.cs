namespace Ecobox.ViewModels;

public sealed class DashboardViewModel
{
    public ResiduoViewModel? UltimoResiduo { get; init; }
    public int TotalResiduos { get; init; }
    public int TotalAprovechables { get; init; }
    public int TotalNoAprovechables { get; init; }
    public ContenedorViewModel ContenedorAprovechables { get; init; } = new();
    public ContenedorViewModel ContenedorNoAprovechables { get; init; } = new();
    public bool BaseDatosDisponible { get; init; }
    public string MensajeBaseDatos { get; init; } = string.Empty;
}
