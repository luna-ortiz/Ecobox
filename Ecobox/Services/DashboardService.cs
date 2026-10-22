using Ecobox.Configuration;
using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Ecobox.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecobox.Services;

public sealed class DashboardService(
    EcoBoxDbContext dbContext,
    IDatabaseStatusService databaseStatus,
    IContenedorService contenedorService,
    IOptions<ContenedorSettings> settings,
    ILogger<DashboardService> logger) : IDashboardService
{
    public async Task<DashboardViewModel> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        if (!await databaseStatus.IsAvailableAsync(cancellationToken))
        {
            return EmptyDashboard(settings.Value, databaseStatus.IsConfigured
                ? "No hay conexión con PostgreSQL. Los datos se mostrarán cuando la base de datos esté disponible."
                : "PostgreSQL aún no está configurado. Se muestran los contenedores iniciales sin registros.");
        }

        try
        {
            var counts = await dbContext.Residuos
                .AsNoTracking()
                .GroupBy(residue => residue.TipoResiduo.Clasificacion)
                .Select(group => new { Clasificacion = group.Key, Total = group.Count() })
                .ToDictionaryAsync(group => group.Clasificacion, group => group.Total, cancellationToken);

            var containers = await dbContext.Contenedores.AsNoTracking().ToListAsync(cancellationToken);
            var latest = await dbContext.Residuos
                .AsNoTracking()
                .Include(residue => residue.TipoResiduo)
                .Include(residue => residue.Contenedor)
                .OrderByDescending(residue => residue.FechaHoraDeteccion)
                .FirstOrDefaultAsync(cancellationToken);

            var aprovechables = containers.FirstOrDefault(container => container.Nombre == "Aprovechables")
                ?? CreateInitialContainer("Aprovechables", settings.Value);
            var noAprovechables = containers.FirstOrDefault(container => container.Nombre == "No aprovechables")
                ?? CreateInitialContainer("No aprovechables", settings.Value);
            var totalAprovechables = counts.GetValueOrDefault(ClasificacionesResiduo.Aprovechable);
            var totalNoAprovechables = counts.GetValueOrDefault(ClasificacionesResiduo.NoAprovechable);

            return new DashboardViewModel
            {
                UltimoResiduo = latest is null ? null : ResiduoViewModel.Desde(latest),
                TotalResiduos = totalAprovechables + totalNoAprovechables,
                TotalAprovechables = totalAprovechables,
                TotalNoAprovechables = totalNoAprovechables,
                ContenedorAprovechables = MapContainer(aprovechables),
                ContenedorNoAprovechables = MapContainer(noAprovechables),
                BaseDatosDisponible = true,
                MensajeBaseDatos = "Datos actualizados desde PostgreSQL. La capacidad es una estimación por conteo de registros."
            };
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "No fue posible cargar el dashboard desde PostgreSQL.");
            return EmptyDashboard(settings.Value, "No fue posible leer PostgreSQL. El dashboard no contiene datos cargados.");
        }
    }

    private DashboardViewModel EmptyDashboard(ContenedorSettings settings, string message)
    {
        var aprovechables = CreateInitialContainer("Aprovechables", settings);
        var noAprovechables = CreateInitialContainer("No aprovechables", settings);
        return new DashboardViewModel
        {
            TotalResiduos = 0,
            TotalAprovechables = 0,
            TotalNoAprovechables = 0,
            ContenedorAprovechables = MapContainer(aprovechables),
            ContenedorNoAprovechables = MapContainer(noAprovechables),
            BaseDatosDisponible = false,
            MensajeBaseDatos = message
        };
    }

    private static Contenedor CreateInitialContainer(string name, ContenedorSettings settings) => new()
    {
        Nombre = name,
        CapacidadMaxima = settings.CapacidadMaxima,
        NivelActual = 0,
        Estado = settings.CalcularEstado(0)
    };

    private ContenedorViewModel MapContainer(Contenedor container) =>
        ContenedorViewModel.Desde(container, contenedorService.CalcularPorcentajeEstimado(container));
}
