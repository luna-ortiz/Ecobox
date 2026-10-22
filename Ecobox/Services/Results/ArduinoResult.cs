namespace Ecobox.Services.Results;

public sealed class ArduinoResult
{
    public bool Exito { get; init; }
    public string Comando { get; init; } = string.Empty;
    public string Mensaje { get; init; } = string.Empty;
}
