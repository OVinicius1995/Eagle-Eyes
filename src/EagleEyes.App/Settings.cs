using System.IO;
using System.Text.Json;
namespace EagleEyes.App;

internal sealed class Settings
{
    public string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Eagle Eyes");
    public string FfmpegDirectory { get; set; } = "";
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
    public string ResolveBinary(string name)
    {
        var candidates = new List<string> { Path.Combine(FfmpegDirectory, name), Path.Combine(AppContext.BaseDirectory, "ffmpeg", name) };
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; directory != null && i < 8; i++, directory = directory.Parent) candidates.Add(Path.Combine(directory.FullName, "components", "ffmpeg-master-latest-win64-gpl", "bin", name));
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.Combine(p, name)));
        return candidates.FirstOrDefault(File.Exists) ?? throw new FileNotFoundException($"Não foi encontrado {name}. Selecione a pasta que contém ffmpeg.exe e ffprobe.exe nas configurações.");
    }
}
