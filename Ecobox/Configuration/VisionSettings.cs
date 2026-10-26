namespace Ecobox.Configuration;

public sealed class VisionSettings
{
    public const string SectionName = "Vision";

    public string ModelPath { get; set; } = string.Empty;
    public double ConfidenceThreshold { get; set; } = 0.70;
}
