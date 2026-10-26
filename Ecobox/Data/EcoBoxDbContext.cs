using Ecobox.Models;
using Microsoft.EntityFrameworkCore;

namespace Ecobox.Data;

public sealed class EcoBoxDbContext(DbContextOptions<EcoBoxDbContext> options) : DbContext(options)
{
    public DbSet<RegistroResiduo> RegistrosResiduo => Set<RegistroResiduo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var registro = modelBuilder.Entity<RegistroResiduo>();
        registro.ToTable("RegistroResiduo");
        registro.HasKey(item => item.Id);
        registro.Property(item => item.Fecha).HasColumnType("TEXT").IsRequired();
        registro.Property(item => item.Caneca).HasConversion<string>().HasMaxLength(30).IsRequired();
        registro.Property(item => item.Cantidad).IsRequired();
        registro.HasIndex(item => new { item.Fecha, item.Caneca }).IsUnique();
    }
}
