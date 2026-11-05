using Ecobox.Models;
using Ecobox.Configuration;

namespace Ecobox.Services.Results;

public sealed record CanecaOcupacionResult(
    CategoriaResiduo Caneca,
    int UnidadesActuales,
    int CapacidadMaxima,
    int PorcentajeOcupacion,
    int UnidadesCicloActuales,
    int UnidadesRestantes,
    int CicloActualId,
    DateTimeOffset CicloIniciadoEn,
    string Estado,
    DateTimeOffset ActualizadoEn,
    DateTimeOffset? UltimoVaciadoEn);

public sealed record CicloCanecaResult(
    int Id,
    CategoriaResiduo Caneca,
    DateTimeOffset IniciadoEn,
    DateTimeOffset? FinalizadoEn,
    int UnidadesRegistradas);

public sealed class CanecaLlenaException(CategoriaResiduo caneca, int ocupacionActual, int cantidadSolicitada)
    : InvalidOperationException($"La caneca {caneca} está llena. Ocupación actual: {ocupacionActual}/{ReglasCapacidadCaneca.CapacidadMaxima}.")
{
    public CategoriaResiduo Caneca { get; } = caneca;
    public int OcupacionActual { get; } = ocupacionActual;
    public int CantidadSolicitada { get; } = cantidadSolicitada;
}

public sealed class CanecaNoEncontradaException(CategoriaResiduo caneca)
    : InvalidOperationException($"No existe un estado persistido para la caneca {caneca}.")
{
    public CategoriaResiduo Caneca { get; } = caneca;
}
