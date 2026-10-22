using Ecobox.Configuration;
using Ecobox.Data;
using Ecobox.Models;
using Ecobox.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Services;

public sealed class ResiduoService(EcoBoxDbContext dbContext, IContenedorService contenedorService) : IResiduoService
{
    public async Task<IReadOnlyList<Residuo>> ObtenerHistorialAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Residuos
            .AsNoTracking()
            .Include(residuo => residuo.TipoResiduo)
            .Include(residuo => residuo.Contenedor)
            .OrderByDescending(residuo => residuo.FechaHoraDeteccion)
            .ToListAsync(cancellationToken);

    public async Task<Residuo> RegistrarAsync(
        string clasificacion,
        int idContenedor,
        decimal confianza,
        string? imagenUrl = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedClassification = clasificacion.Trim().ToUpperInvariant();
        if (!ClasificacionesResiduo.Permitidas.Contains(normalizedClassification, StringComparer.Ordinal))
        {
            throw new ArgumentException("La clasificación debe ser APROVECHABLE o NO_APROVECHABLE.", nameof(clasificacion));
        }

        if (confianza is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confianza), "La confianza debe estar entre 0 y 1.");
        }

        var tipo = await dbContext.TiposResiduo.SingleAsync(
            item => item.Clasificacion == normalizedClassification,
            cancellationToken);
        var contenedor = await contenedorService.ObtenerPorIdAsync(idContenedor, cancellationToken)
            ?? throw new InvalidOperationException("No se encontró el contenedor seleccionado.");

        var expectedContainerName = normalizedClassification == ClasificacionesResiduo.Aprovechable
            ? "Aprovechables"
            : "No aprovechables";
        if (!string.Equals(contenedor.Nombre, expectedContainerName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("La clasificación debe registrarse en su contenedor correspondiente.");
        }

        var residue = new Residuo
        {
            IdTipo = tipo.IdTipo,
            IdContenedor = contenedor.IdContenedor,
            FechaHoraDeteccion = DateTime.UtcNow,
            ConfianzaClasificacion = confianza,
            ImagenUrl = imagenUrl
        };

        dbContext.Residuos.Add(residue);
        contenedorService.ActualizarNivel(contenedor, 1);
        await dbContext.SaveChangesAsync(cancellationToken);
        return residue;
    }

    public async Task<bool> EliminarAsync(int idResiduo, CancellationToken cancellationToken = default)
    {
        var residue = await dbContext.Residuos
            .Include(item => item.Contenedor)
            .SingleOrDefaultAsync(item => item.IdResiduo == idResiduo, cancellationToken);
        if (residue is null)
        {
            return false;
        }

        contenedorService.ActualizarNivel(residue.Contenedor, -1);
        dbContext.Residuos.Remove(residue);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> MoverAsync(int idResiduo, int nuevoIdContenedor, CancellationToken cancellationToken = default)
    {
        var residue = await dbContext.Residuos
            .Include(item => item.Contenedor)
            .Include(item => item.TipoResiduo)
            .SingleOrDefaultAsync(item => item.IdResiduo == idResiduo, cancellationToken);
        if (residue is null)
        {
            return false;
        }

        if (residue.IdContenedor == nuevoIdContenedor)
        {
            return true;
        }

        var nextContainer = await contenedorService.ObtenerPorIdAsync(nuevoIdContenedor, cancellationToken)
            ?? throw new InvalidOperationException("No se encontró el nuevo contenedor.");
        contenedorService.ActualizarNivel(residue.Contenedor, -1);
        contenedorService.ActualizarNivel(nextContainer, 1);
        residue.Contenedor = nextContainer;
        residue.IdContenedor = nextContainer.IdContenedor;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
