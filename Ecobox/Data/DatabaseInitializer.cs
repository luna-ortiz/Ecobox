using Ecobox.Configuration;
using Ecobox.Models;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(EcoBoxDbContext dbContext, ContenedorSettings settings, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        foreach (var classification in ClasificacionesResiduo.Permitidas)
        {
            if (!await dbContext.TiposResiduo.AnyAsync(tipo => tipo.Clasificacion == classification, cancellationToken))
            {
                dbContext.TiposResiduo.Add(new TipoResiduo { Clasificacion = classification });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var initialContainers = new[]
        {
            "Aprovechables",
            "No aprovechables"
        };

        foreach (var name in initialContainers)
        {
            if (!await dbContext.Contenedores.AnyAsync(contenedor => contenedor.Nombre == name, cancellationToken))
            {
                dbContext.Contenedores.Add(new Contenedor
                {
                    Nombre = name,
                    CapacidadMaxima = settings.CapacidadMaxima,
                    NivelActual = 0,
                    Estado = settings.CalcularEstado(0)
                });
            }
        }

        var containers = await dbContext.Contenedores.ToListAsync(cancellationToken);
        foreach (var container in containers)
        {
            container.NivelActual = await dbContext.Residuos.CountAsync(
                residue => residue.IdContenedor == container.IdContenedor,
                cancellationToken);
            container.Estado = settings.CalcularEstado(container.NivelActual);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
