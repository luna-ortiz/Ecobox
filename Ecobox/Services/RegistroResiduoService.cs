using Ecobox.Data;
using Ecobox.Configuration;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Ecobox.Services.Results;

namespace Ecobox.Services;

public sealed class RegistroResiduoService(EcoBoxDbContext dbContext) : IRegistroResiduoService
{
    public async Task<IReadOnlyList<RegistroResiduo>> ObtenerHistorialAsync(CancellationToken cancellationToken = default) =>
        await dbContext.RegistrosResiduo
            .AsNoTracking()
            .OrderByDescending(registro => registro.Fecha)
            .ThenByDescending(registro => registro.CicloId)
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

        var estadoCaneca = await dbContext.EstadosCanecas.SingleOrDefaultAsync(
            estado => estado.Caneca == caneca,
            cancellationToken);
        if (estadoCaneca is null)
        {
            throw new CanecaNoEncontradaException(caneca);
        }

        if (cantidad > ReglasCapacidadCaneca.CapacidadMaxima - estadoCaneca.UnidadesActuales)
        {
            throw new CanecaLlenaException(caneca, estadoCaneca.UnidadesActuales, cantidad);
        }

        if (estadoCaneca.CicloActualId <= 0)
        {
            throw new InvalidOperationException($"La caneca {caneca} no tiene un ciclo de llenado activo.");
        }

        var registro = await dbContext.RegistrosResiduo.SingleOrDefaultAsync(
            item => item.Fecha == fechaRegistro && item.Caneca == caneca && item.CicloId == estadoCaneca.CicloActualId,
            cancellationToken);

        if (registro is null)
        {
            registro = new RegistroResiduo
            {
                Fecha = fechaRegistro,
                Caneca = caneca,
                Cantidad = cantidad,
                CicloId = estadoCaneca.CicloActualId
            };
            dbContext.RegistrosResiduo.Add(registro);
        }
        else
        {
            registro.Cantidad = checked(registro.Cantidad + cantidad);
        }

        estadoCaneca.UnidadesActuales += cantidad;
        estadoCaneca.ActualizadoEn = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return registro;
    }

    public async Task<IReadOnlyList<CanecaOcupacionResult>> ObtenerOcupacionesAsync(CancellationToken cancellationToken = default)
    {
        var estados = await dbContext.EstadosCanecas
            .AsNoTracking()
            .OrderBy(estado => estado.Caneca)
            .ToListAsync(cancellationToken);

        var cycleIds = estados.Select(estado => estado.CicloActualId).Where(id => id > 0).ToArray();
        var ciclos = await dbContext.CiclosCanecas.AsNoTracking()
            .Where(ciclo => cycleIds.Contains(ciclo.Id))
            .ToDictionaryAsync(ciclo => ciclo.Id, cancellationToken);
        var unidadesPorCiclo = await dbContext.RegistrosResiduo.AsNoTracking()
            .Where(registro => cycleIds.Contains(registro.CicloId))
            .GroupBy(registro => registro.CicloId)
            .Select(grupo => new { CicloId = grupo.Key, Total = grupo.Sum(registro => registro.Cantidad) })
            .ToDictionaryAsync(item => item.CicloId, item => item.Total, cancellationToken);

        return estados.Select(estado =>
        {
            ciclos.TryGetValue(estado.CicloActualId, out var cicloActual);
            unidadesPorCiclo.TryGetValue(estado.CicloActualId, out var registradas);
            return new CanecaOcupacionResult(
                estado.Caneca,
                estado.UnidadesActuales,
                ReglasCapacidadCaneca.CapacidadMaxima,
                ReglasCapacidadCaneca.CalcularPorcentaje(estado.UnidadesActuales),
                registradas,
                ReglasCapacidadCaneca.CapacidadMaxima - estado.UnidadesActuales,
                estado.CicloActualId,
                cicloActual?.IniciadoEn ?? estado.ActualizadoEn,
                ReglasCapacidadCaneca.CalcularEstado(estado.UnidadesActuales),
                estado.ActualizadoEn,
                estado.UltimoVaciadoEn);
        }).ToArray();
    }

    public async Task<IReadOnlyList<CicloCanecaResult>> ObtenerCiclosAsync(CancellationToken cancellationToken = default)
    {
        var ciclos = await dbContext.CiclosCanecas.AsNoTracking()
            .ToListAsync(cancellationToken);
        var ids = ciclos.Select(ciclo => ciclo.Id).ToArray();
        var totales = await dbContext.RegistrosResiduo.AsNoTracking()
            .Where(registro => ids.Contains(registro.CicloId))
            .GroupBy(registro => registro.CicloId)
            .Select(grupo => new { CicloId = grupo.Key, Total = grupo.Sum(registro => registro.Cantidad) })
            .ToDictionaryAsync(item => item.CicloId, item => item.Total, cancellationToken);

        return ciclos.OrderByDescending(ciclo => ciclo.IniciadoEn).Select(ciclo => new CicloCanecaResult(
            ciclo.Id,
            ciclo.Caneca,
            ciclo.IniciadoEn,
            ciclo.FinalizadoEn,
            totales.GetValueOrDefault(ciclo.Id))).ToArray();
    }

    public async Task VaciarAsync(CategoriaResiduo caneca, CancellationToken cancellationToken = default)
    {
        ValidarCaneca(caneca);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var estado = await dbContext.EstadosCanecas.SingleOrDefaultAsync(item => item.Caneca == caneca, cancellationToken);
        if (estado is null)
        {
            throw new CanecaNoEncontradaException(caneca);
        }

        var ahora = DateTimeOffset.UtcNow;
        var cicloActual = await dbContext.CiclosCanecas.SingleOrDefaultAsync(
            ciclo => ciclo.Id == estado.CicloActualId && ciclo.Caneca == caneca && ciclo.FinalizadoEn == null,
            cancellationToken);
        if (cicloActual is null)
        {
            throw new InvalidOperationException($"La caneca {caneca} no tiene un ciclo activo que pueda cerrarse.");
        }

        cicloActual.FinalizadoEn = ahora;
        var siguienteCiclo = new CicloCaneca { Caneca = caneca, IniciadoEn = ahora };
        dbContext.CiclosCanecas.Add(siguienteCiclo);
        estado.UnidadesActuales = 0;
        estado.ActualizadoEn = ahora;
        estado.UltimoVaciadoEn = ahora;
        estado.CicloActualId = 0;
        await dbContext.SaveChangesAsync(cancellationToken);
        estado.CicloActualId = siguienteCiclo.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidarCaneca(CategoriaResiduo caneca)
    {
        if (!Enum.IsDefined(caneca))
        {
            throw new ArgumentOutOfRangeException(nameof(caneca), "La caneca debe ser APROVECHABLE o NO_APROVECHABLE.");
        }
    }
}
