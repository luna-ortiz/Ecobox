namespace Ecobox.Services.Results;

public sealed class VisionResult
{
    public string? Categoria { get; init; }
    public string? Prediccion { get; init; }
    public string? RoiHash { get; init; }
    public string? Diagnostico { get; init; }
    public decimal? Confianza { get; init; }
    public bool Exito { get; init; }
    public string Mensaje { get; init; } = string.Empty;
}
