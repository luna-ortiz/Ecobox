namespace Ecobox.Configuration;

public sealed class VisionSettings
{
    public const string SectionName = "Vision";

    public string PythonExecutable { get; set; } = "python";
    public string ScriptPath { get; set; } = "AI/Python/vision_server.py";
    public string ModelPath { get; set; } = "AI/Python/ecobox_modelo.keras";
    public string ClassesPath { get; set; } = "AI/Python/clases.json";
    public double ConfidenceThreshold { get; set; } = 0.70;
    public int FrameIntervalMs { get; set; } = 200;
    public int StableFramesRequired { get; set; } = 2;
    public int DetectionCooldownMs { get; set; } = 2500;
    public double PresenceChangeThreshold { get; set; } = 0.10;
    public double RoiSizeRatio { get; set; } = 0.85;
    public int StableFrameHashDistance { get; set; } = 18;
    public bool SaveDebugFrames { get; set; }
    public string DebugFrameDirectory { get; set; } = string.Empty;
    public int DebugFrameLimit { get; set; } = 8;
}
