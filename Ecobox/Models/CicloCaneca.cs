namespace Ecobox.Models;

public sealed class CicloCaneca
{
    public int Id { get; set; }
    public CategoriaResiduo Caneca { get; set; }
    public DateTimeOffset IniciadoEn { get; set; }
    public DateTimeOffset? FinalizadoEn { get; set; }
}
