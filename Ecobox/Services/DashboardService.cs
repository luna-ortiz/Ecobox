using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Ecobox.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Services;

public sealed class DashboardService(EcoBoxDbContext dbContext, IRegistroResiduoService registroService) : IDashboardService
{
    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var ocupaciones = await registroService.ObtenerOcupacionesAsync(cancellationToken);
        var cycleIds = ocupaciones.Select(ocupacion => ocupacion.CicloActualId).ToArray();
        var registros = await dbContext.RegistrosResiduo.AsNoTracking()
            .Where(registro => cycleIds.Contains(registro.CicloId))
            .ToListAsync(cancellationToken);
        var aprovechables = ocupaciones
            .Where(ocupacion => ocupacion.Caneca == CategoriaResiduo.APROVECHABLE)
            .Sum(ocupacion => ocupacion.UnidadesCicloActuales);
        var noAprovechables = ocupaciones
            .Where(ocupacion => ocupacion.Caneca == CategoriaResiduo.NO_APROVECHABLE)
            .Sum(ocupacion => ocupacion.UnidadesCicloActuales);
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
            OcupacionesCanecas = ocupaciones,
            MensajeBaseDatos = "Base local SQLite conectada."
        };
    }
}
