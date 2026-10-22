using System.ComponentModel.DataAnnotations;

namespace Ecobox.Models;

public class TipoResiduo
{
    public int IdTipo { get; set; }

    [Required, StringLength(30)]
    public string Clasificacion { get; set; } = string.Empty;

    public ICollection<Residuo> Residuos { get; set; } = new List<Residuo>();
}
