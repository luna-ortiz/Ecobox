using Ecobox.Models;

namespace Ecobox.Services.Interfaces;

public interface IContenedorService
{
    Task<IReadOnlyList<Contenedor>> ObtenerTodosAsync(CancellationToken cancellationToken = default);
    Task<Contenedor?> ObtenerPorIdAsync(int idContenedor, CancellationToken cancellationToken = default);
    void ActualizarNivel(Contenedor contenedor, int cambio);
    decimal CalcularPorcentajeEstimado(Contenedor contenedor);
}
