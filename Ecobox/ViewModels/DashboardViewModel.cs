namespace Ecobox.ViewModels;

public sealed class DashboardViewModel
{
    public RegistroResiduoViewModel? UltimoRegistro { get; init; }
    public int TotalResiduos { get; init; }
    public int TotalAprovechables { get; init; }
    public int TotalNoAprovechables { get; init; }
    public string MensajeBaseDatos { get; init; } = string.Empty;
}
