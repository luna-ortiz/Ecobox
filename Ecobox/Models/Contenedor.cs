using System.ComponentModel.DataAnnotations;

namespace Ecobox.Models;

public class Contenedor
{
    public int IdContenedor { get; set; }

    [Required, StringLength(80)]
    public string Nombre { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CapacidadMaxima { get; set; }

    [Range(0, int.MaxValue)]
    public int NivelActual { get; set; }

    [Required, StringLength(30)]
    public string Estado { get; set; } = string.Empty;

    public ICollection<Residuo> Residuos { get; set; } = new List<Residuo>();
}
