namespace Ecobox.ViewModels;

public sealed class CamaraViewModel
{
    public string EstadoDeteccion { get; init; } = "Esperando residuo...";
    public string UltimaClasificacion { get; init; } = "Sin clasificaciones";
    public string UltimaConfianza { get; init; } = "—";
}
