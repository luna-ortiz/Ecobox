using Ecobox.Models;

namespace Ecobox.ViewModels;

public sealed class ContenedorViewModel
{
    public int IdContenedor { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public int NivelActual { get; init; }
    public int CapacidadMaxima { get; init; }
    public decimal PorcentajeEstimado { get; init; }
    public string Estado { get; init; } = string.Empty;

    public static ContenedorViewModel Desde(Contenedor contenedor, decimal porcentajeEstimado) => new()
    {
        IdContenedor = contenedor.IdContenedor,
        Nombre = contenedor.Nombre,
        NivelActual = contenedor.NivelActual,
        CapacidadMaxima = contenedor.CapacidadMaxima,
        PorcentajeEstimado = porcentajeEstimado,
        Estado = contenedor.Estado
    };
}
