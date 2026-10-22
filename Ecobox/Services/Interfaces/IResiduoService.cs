using Ecobox.Models;

namespace Ecobox.Services.Interfaces;

public interface IResiduoService
{
    Task<IReadOnlyList<Residuo>> ObtenerHistorialAsync(CancellationToken cancellationToken = default);
    Task<Residuo> RegistrarAsync(string clasificacion, int idContenedor, decimal confianza, string? imagenUrl = null, CancellationToken cancellationToken = default);
    Task<bool> EliminarAsync(int idResiduo, CancellationToken cancellationToken = default);
    Task<bool> MoverAsync(int idResiduo, int nuevoIdContenedor, CancellationToken cancellationToken = default);
}
