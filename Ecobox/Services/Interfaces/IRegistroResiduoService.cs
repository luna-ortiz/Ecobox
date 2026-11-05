using Ecobox.Models;
using Ecobox.Services.Results;

namespace Ecobox.Services.Interfaces;

public interface IRegistroResiduoService
{
    Task<IReadOnlyList<RegistroResiduo>> ObtenerHistorialAsync(CancellationToken cancellationToken = default);

    Task<RegistroResiduo> RegistrarAsync(
        CategoriaResiduo caneca,
        int cantidad = 1,
        DateOnly? fecha = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CanecaOcupacionResult>> ObtenerOcupacionesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CicloCanecaResult>> ObtenerCiclosAsync(CancellationToken cancellationToken = default);

    Task VaciarAsync(CategoriaResiduo caneca, CancellationToken cancellationToken = default);
}
