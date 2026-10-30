using Ecobox.Models;

namespace Ecobox.Services.Results;

public sealed class ClasificacionResiduoResult
{
    public bool Exito { get; init; }
    public bool RegistroGuardado { get; init; }
    public string Estado { get; init; } = "error";
    public string? Razon { get; init; }
    public CategoriaResiduo? Categoria { get; init; }
    public decimal? Confianza { get; init; }
    public string Mensaje { get; init; } = string.Empty;
}
