using Ecobox.Configuration;
using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecobox.Services;

public sealed class ContenedorService(EcoBoxDbContext dbContext, IOptions<ContenedorSettings> settings) : IContenedorService
{
    public async Task<IReadOnlyList<Contenedor>> ObtenerTodosAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Contenedores
            .AsNoTracking()
            .OrderBy(contenedor => contenedor.Nombre)
            .ToListAsync(cancellationToken);

    public Task<Contenedor?> ObtenerPorIdAsync(int idContenedor, CancellationToken cancellationToken = default) =>
        dbContext.Contenedores.SingleOrDefaultAsync(contenedor => contenedor.IdContenedor == idContenedor, cancellationToken);

    public void ActualizarNivel(Contenedor contenedor, int cambio)
    {
        contenedor.NivelActual = Math.Max(0, contenedor.NivelActual + cambio);
        contenedor.Estado = settings.Value.CalcularEstado(contenedor.NivelActual);
    }

    public decimal CalcularPorcentajeEstimado(Contenedor contenedor) => contenedor.CapacidadMaxima <= 0
        ? 0
        : decimal.Round((decimal)contenedor.NivelActual / contenedor.CapacidadMaxima * 100, 1);
}
