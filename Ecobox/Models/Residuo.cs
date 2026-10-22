using System.ComponentModel.DataAnnotations;

namespace Ecobox.Models;

public class Residuo
{
    public int IdResiduo { get; set; }
    public int IdTipo { get; set; }
    public int IdContenedor { get; set; }

    public DateTime FechaHoraDeteccion { get; set; }

    [Range(typeof(decimal), "0", "1")]
    public decimal ConfianzaClasificacion { get; set; }

    [StringLength(2048)]
    public string? ImagenUrl { get; set; }

    public TipoResiduo TipoResiduo { get; set; } = null!;
    public Contenedor Contenedor { get; set; } = null!;
}
