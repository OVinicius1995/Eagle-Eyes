using System.IO;
namespace EagleEyes.App;

internal static class FFmpegLocation
{
    internal static string Resolve(string name, string? applicationDirectory = null)
    {
        if (!string.Equals(name, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(name, "ffprobe.exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Componente de vídeo desconhecido.", nameof(name));
        string root = applicationDirectory ?? AppContext.BaseDirectory;
        if (!Path.IsPathFullyQualified(root)) throw new ArgumentException("A pasta do aplicativo deve ser absoluta.", nameof(applicationDirectory));
        root = Path.GetFullPath(root);
        var candidates = new List<string> { Path.Combine(root, "engine", "ffmpeg-9.0.2-essentials_build", "bin"), Path.Combine(root, "ffmpeg") };
        var ancestor = new DirectoryInfo(root);
        for (int depth = 0; ancestor != null && depth < 8; depth++, ancestor = ancestor.Parent)
            candidates.Add(Path.Combine(ancestor.FullName, "components", "ffmpeg-master-latest-win64-gpl", "bin"));
        foreach (var folder in candidates)
        {
            if (File.Exists(Path.Combine(folder, "ffmpeg.exe")) && File.Exists(Path.Combine(folder, "ffprobe.exe")))
                return Path.Combine(folder, name.ToLowerInvariant());
        }
        throw new FileNotFoundException("Os componentes de gravação não foram encontrados. Coloque os arquivos ffmpeg.exe e ffprobe.exe juntos na pasta ffmpeg, ao lado do Eagle Eyes, e tente gravar novamente.");
    }
}

