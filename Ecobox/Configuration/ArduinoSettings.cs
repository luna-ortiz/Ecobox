namespace Ecobox.Configuration;

public sealed class ArduinoSettings
{
    public const string SectionName = "Arduino";

    public string Port { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 9600;
}
