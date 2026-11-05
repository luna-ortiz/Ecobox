using Ecobox.Models;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Data;

public sealed class EcoBoxDbContext(DbContextOptions<EcoBoxDbContext> options) : DbContext(options)
{
    public DbSet<RegistroResiduo> RegistrosResiduo => Set<RegistroResiduo>();
    public DbSet<EstadoCaneca> EstadosCanecas => Set<EstadoCaneca>();
    public DbSet<CicloCaneca> CiclosCanecas => Set<CicloCaneca>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var registro = modelBuilder.Entity<RegistroResiduo>();
        registro.ToTable("RegistroResiduo");
        registro.HasKey(item => item.Id);
        registro.Property(item => item.Fecha).HasColumnType("TEXT").IsRequired();
        registro.Property(item => item.Caneca).HasConversion<string>().HasMaxLength(30).IsRequired();
        registro.Property(item => item.Cantidad).IsRequired();
        registro.Property(item => item.CicloId).IsRequired();
        registro.HasIndex(item => new { item.CicloId, item.Fecha, item.Caneca }).IsUnique();

        var ciclo = modelBuilder.Entity<CicloCaneca>();
        ciclo.ToTable("CicloCaneca");
        ciclo.HasKey(item => item.Id);
        ciclo.Property(item => item.Caneca).HasConversion<string>().HasMaxLength(30).IsRequired();
        ciclo.Property(item => item.IniciadoEn).HasColumnType("TEXT").IsRequired();
        ciclo.Property(item => item.FinalizadoEn).HasColumnType("TEXT");
        ciclo.HasIndex(item => new { item.Caneca, item.FinalizadoEn });

        var estadoCaneca = modelBuilder.Entity<EstadoCaneca>();
        estadoCaneca.ToTable("EstadoCaneca", table => table.HasCheckConstraint(
            "CK_EstadoCaneca_UnidadesActuales",
            "\"UnidadesActuales\" >= 0 AND \"UnidadesActuales\" <= 50"));
        estadoCaneca.HasKey(item => item.Caneca);
        estadoCaneca.Property(item => item.Caneca).HasConversion<string>().HasMaxLength(30).IsRequired();
        estadoCaneca.Property(item => item.UnidadesActuales).IsRequired();
        estadoCaneca.Property(item => item.ActualizadoEn).HasColumnType("TEXT").IsRequired();
        estadoCaneca.Property(item => item.UltimoVaciadoEn).HasColumnType("TEXT");
        estadoCaneca.Property(item => item.CicloActualId).HasDefaultValue(0).IsRequired();
    }
}
