using System.ComponentModel.DataAnnotations;

namespace Ecobox.Models;

public sealed class RegistroResiduo
{
    public int Id { get; set; }

    public DateOnly Fecha { get; set; }

    public CategoriaResiduo Caneca { get; set; }

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; }
}
