namespace Ecobox.Configuration;

public sealed class ContenedorSettings
{
    public const string SectionName = "ContenedorSettings";

    public int CapacidadMaxima { get; set; } = 50;
    public int NivelMaximoDisponible { get; set; } = 40;
    public int NivelMaximoPorLlenarse { get; set; } = 50;

    public string CalcularEstado(int nivelActual)
    {
        if (nivelActual <= 0)
        {
            return EstadosContenedor.Vacia;
        }

        if (nivelActual <= NivelMaximoDisponible)
        {
            return EstadosContenedor.Disponible;
        }

        if (nivelActual <= NivelMaximoPorLlenarse)
        {
            return EstadosContenedor.PorLlenarse;
        }

        return EstadosContenedor.RequiereCambio;
    }
}

public static class EstadosContenedor
{
    public const string Vacia = "VACÍA";
    public const string Disponible = "DISPONIBLE";
    public const string PorLlenarse = "POR LLENARSE";
    public const string RequiereCambio = "REQUIERE CAMBIO";
}

public static class ClasificacionesResiduo
{
    public const string Aprovechable = "APROVECHABLE";
    public const string NoAprovechable = "NO_APROVECHABLE";

    public static readonly string[] Permitidas = [Aprovechable, NoAprovechable];
}
