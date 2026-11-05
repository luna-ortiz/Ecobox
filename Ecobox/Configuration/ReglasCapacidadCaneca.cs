namespace Ecobox.Configuration;

public static class ReglasCapacidadCaneca
{
    public const int CapacidadMaxima = 50;
    public const int UmbralProximaALlenarse = 45;

    public static int CalcularPorcentaje(int unidadesActuales)
    {
        if (unidadesActuales is < 0 or > CapacidadMaxima)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesActuales));
        }

        return (int)Math.Round(unidadesActuales / (double)CapacidadMaxima * 100);
    }

    public static string CalcularEstado(int unidadesActuales)
    {
        if (unidadesActuales is < 0 or > CapacidadMaxima)
        {
            throw new ArgumentOutOfRangeException(nameof(unidadesActuales));
        }

        return unidadesActuales switch
        {
            0 => "Vacía",
            CapacidadMaxima => "Llena",
            >= UmbralProximaALlenarse => "Próxima a llenarse",
            _ => "Disponible"
        };
    }
}
