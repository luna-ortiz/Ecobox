using Ecobox.Models;

namespace Ecobox.ViewModels;

public sealed class ResiduoViewModel
{
    public int IdResiduo { get; init; }
    public DateTime FechaHoraDeteccion { get; init; }
    public string Clasificacion { get; init; } = string.Empty;
    public string Contenedor { get; init; } = string.Empty;
    public decimal ConfianzaClasificacion { get; init; }
    public string? ImagenUrl { get; init; }

    public static ResiduoViewModel Desde(Residuo residuo) => new()
    {
        IdResiduo = residuo.IdResiduo,
        FechaHoraDeteccion = residuo.FechaHoraDeteccion,
        Clasificacion = residuo.TipoResiduo.Clasificacion,
        Contenedor = residuo.Contenedor.Nombre,
        ConfianzaClasificacion = residuo.ConfianzaClasificacion,
        ImagenUrl = residuo.ImagenUrl
    };
}
