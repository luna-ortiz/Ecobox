using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Services;

public sealed class RegistroResiduoService(EcoBoxDbContext dbContext) : IRegistroResiduoService
{
    public async Task<IReadOnlyList<RegistroResiduo>> ObtenerHistorialAsync(CancellationToken cancellationToken = default) =>
        await dbContext.RegistrosResiduo
            .AsNoTracking()
            .OrderByDescending(registro => registro.Fecha)
            .ThenBy(registro => registro.Caneca)
            .ToListAsync(cancellationToken);

    public async Task<RegistroResiduo> RegistrarAsync(
        CategoriaResiduo caneca,
        int cantidad = 1,
        DateOnly? fecha = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(caneca))
        {
            throw new ArgumentOutOfRangeException(nameof(caneca), "La caneca debe ser APROVECHABLE o NO_APROVECHABLE.");
        }

        if (cantidad <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cantidad), "La cantidad debe ser mayor que cero.");
        }

        var fechaRegistro = fecha ?? DateOnly.FromDateTime(DateTime.Now);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var registro = await dbContext.RegistrosResiduo.SingleOrDefaultAsync(
            item => item.Fecha == fechaRegistro && item.Caneca == caneca,
            cancellationToken);

        if (registro is null)
        {
            registro = new RegistroResiduo
            {
                Fecha = fechaRegistro,
                Caneca = caneca,
                Cantidad = cantidad
            };
            dbContext.RegistrosResiduo.Add(registro);
        }
        else
        {
            registro.Cantidad = checked(registro.Cantidad + cantidad);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return registro;
    }
}
