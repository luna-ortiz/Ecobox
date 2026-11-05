namespace Ecobox.Models;

public sealed class EstadoCaneca
{
    public CategoriaResiduo Caneca { get; set; }
    public int UnidadesActuales { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
    public DateTimeOffset? UltimoVaciadoEn { get; set; }
    public int CicloActualId { get; set; }
}
