using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Ecobox.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Services;

public sealed class DashboardService(EcoBoxDbContext dbContext) : IDashboardService
{
    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var registros = await dbContext.RegistrosResiduo.AsNoTracking().ToListAsync(cancellationToken);
        var aprovechables = registros
            .Where(registro => registro.Caneca == CategoriaResiduo.APROVECHABLE)
            .Sum(registro => registro.Cantidad);
        var noAprovechables = registros
            .Where(registro => registro.Caneca == CategoriaResiduo.NO_APROVECHABLE)
            .Sum(registro => registro.Cantidad);
        var ultimo = registros
            .OrderByDescending(registro => registro.Fecha)
            .ThenBy(registro => registro.Caneca)
            .FirstOrDefault();

        return new DashboardViewModel
        {
            UltimoRegistro = ultimo is null ? null : RegistroResiduoViewModel.Desde(ultimo),
            TotalResiduos = aprovechables + noAprovechables,
            TotalAprovechables = aprovechables,
            TotalNoAprovechables = noAprovechables,
            MensajeBaseDatos = "Base local SQLite conectada."
        };
    }
}
