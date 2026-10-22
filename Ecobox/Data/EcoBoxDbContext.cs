using Ecobox.Models;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Data;

public class EcoBoxDbContext(DbContextOptions<EcoBoxDbContext> options) : DbContext(options)
{
    public DbSet<TipoResiduo> TiposResiduo => Set<TipoResiduo>();
    public DbSet<Contenedor> Contenedores => Set<Contenedor>();
    public DbSet<Residuo> Residuos => Set<Residuo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TipoResiduo>(entity =>
        {
            entity.ToTable("tipo_residuo", table => table.HasCheckConstraint(
                "ck_tipo_residuo_clasificacion",
                "clasificacion IN ('APROVECHABLE', 'NO_APROVECHABLE')"));
            entity.HasKey(tipo => tipo.IdTipo);
            entity.Property(tipo => tipo.IdTipo).HasColumnName("id_tipo");
            entity.Property(tipo => tipo.Clasificacion).HasColumnName("clasificacion").HasMaxLength(30).IsRequired();
            entity.HasIndex(tipo => tipo.Clasificacion).IsUnique();
        });

        modelBuilder.Entity<Contenedor>(entity =>
        {
            entity.ToTable("contenedor", table => table.HasCheckConstraint(
                "ck_contenedor_niveles",
                "capacidad_maxima > 0 AND nivel_actual >= 0"));
            entity.HasKey(contenedor => contenedor.IdContenedor);
            entity.Property(contenedor => contenedor.IdContenedor).HasColumnName("id_contenedor");
            entity.Property(contenedor => contenedor.Nombre).HasColumnName("nombre").HasMaxLength(80).IsRequired();
            entity.Property(contenedor => contenedor.CapacidadMaxima).HasColumnName("capacidad_maxima").IsRequired();
            entity.Property(contenedor => contenedor.NivelActual).HasColumnName("nivel_actual").IsRequired();
            entity.Property(contenedor => contenedor.Estado).HasColumnName("estado").HasMaxLength(30).IsRequired();
            entity.HasIndex(contenedor => contenedor.Nombre).IsUnique();
        });

        modelBuilder.Entity<Residuo>(entity =>
        {
            entity.ToTable("residuo", table => table.HasCheckConstraint(
                "ck_residuo_confianza",
                "confianza_clasificacion >= 0 AND confianza_clasificacion <= 1"));
            entity.HasKey(residuo => residuo.IdResiduo);
            entity.Property(residuo => residuo.IdResiduo).HasColumnName("id_residuo");
            entity.Property(residuo => residuo.IdTipo).HasColumnName("id_tipo");
            entity.Property(residuo => residuo.IdContenedor).HasColumnName("id_contenedor");
            entity.Property(residuo => residuo.FechaHoraDeteccion).HasColumnName("fecha_hora_deteccion").HasColumnType("timestamp with time zone");
            entity.Property(residuo => residuo.ConfianzaClasificacion).HasColumnName("confianza_clasificacion").HasPrecision(5, 4);
            entity.Property(residuo => residuo.ImagenUrl).HasColumnName("imagen_url").HasMaxLength(2048);

            entity.HasOne(residuo => residuo.TipoResiduo)
                .WithMany(tipo => tipo.Residuos)
                .HasForeignKey(residuo => residuo.IdTipo)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(residuo => residuo.Contenedor)
                .WithMany(contenedor => contenedor.Residuos)
                .HasForeignKey(residuo => residuo.IdContenedor)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(residuo => residuo.FechaHoraDeteccion);
        });
    }
}
