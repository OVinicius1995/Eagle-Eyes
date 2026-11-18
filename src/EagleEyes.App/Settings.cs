using System.IO;
using System.Text.Json;
namespace EagleEyes.App;

internal sealed class Settings
{
    public string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Eagle Eyes");
    public string MonitorDevice { get; set; } = "";
    public string Edge { get; set; } = "Right";
    public double Offset { get; set; } = 0.45;
    public bool Expanded { get; set; } = true;
    public bool KeepExpanded { get; set; } = true;
    public bool FullMonitor { get; set; }
    public bool HideDuringCapture { get; set; } = true;
    public int Countdown { get; set; } = 3;
    static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EagleEyes", "settings.json");
    public static Settings Load() { try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); } }
    public void Save() { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); var temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, FilePath, true); }
    public string ResolveBinary(string name) => FFmpegLocation.Resolve(name);
}


