using Ecobox.Models;

namespace Ecobox.Services.Interfaces;

public interface IRegistroResiduoService
{
    Task<IReadOnlyList<RegistroResiduo>> ObtenerHistorialAsync(CancellationToken cancellationToken = default);

    Task<RegistroResiduo> RegistrarAsync(
        CategoriaResiduo caneca,
        int cantidad = 1,
        DateOnly? fecha = null,
        CancellationToken cancellationToken = default);
}
