using Ecobox.Models;

namespace Ecobox.ViewModels;

public sealed class RegistroResiduoViewModel
{
    public DateOnly Fecha { get; init; }
    public CategoriaResiduo Caneca { get; init; }
    public int Cantidad { get; init; }

    public string NombreCaneca => Caneca == CategoriaResiduo.APROVECHABLE
        ? "Aprovechable"
        : "No aprovechable";

    public static RegistroResiduoViewModel Desde(RegistroResiduo registro) => new()
    {
        Fecha = registro.Fecha,
        Caneca = registro.Caneca,
        Cantidad = registro.Cantidad
    };
}
