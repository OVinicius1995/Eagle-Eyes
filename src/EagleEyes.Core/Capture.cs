using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace EagleEyes.Core;

public sealed record CaptureArea(int X, int Y, int Width, int Height);
public sealed record DiscardResult(bool Complete, IReadOnlyList<string> RemainingPaths);
public enum RecordingState { Idle, Recording, Paused, Finalizing, Faulted }

public sealed class RecordingSession : IDisposable
{
    readonly string ffmpegPath, ffprobePath;
    readonly SemaphoreSlim gate = new(1, 1);
    readonly List<string> segments = [];
    readonly HashSet<string> ownedFiles = new(StringComparer.OrdinalIgnoreCase);
    string? ownedDirectory, ownershipId;
    bool discardable;
    long segmentMicroseconds, captureGeneration;
    readonly object logLock = new();
    readonly StringBuilder errors = new();
    Process? capture;
    Task? stderrTask, progressTask;
    CancellationTokenSource? captureReaders;
    CaptureArea? area;
    string outputDirectory = "";
    TimeSpan completed;
    bool disposed;
    RecordingState state = RecordingState.Idle;
    public RecordingState State
    {
        get
        {
            if (state == RecordingState.Recording && capture is { HasExited: true })
            {
                state = RecordingState.Faulted;
            }
            return state;
        }
        private set => state = value;
    }
    public TimeSpan RecordedDuration => completed + TimeSpan.FromTicks(Interlocked.Read(ref segmentMicroseconds) * 10);
    public string? RecoveryDirectory { get; private set; }

    public RecordingSession(string ffmpegPath, string ffprobePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ffmpegPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(ffprobePath);
        this.ffmpegPath = ffmpegPath; this.ffprobePath = ffprobePath;
    }
    async Task Locked(Func<Task> action)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!await gate.WaitAsync(0)) throw new InvalidOperationException("Outro comando está em andamento.");
        try { await action(); } finally { gate.Release(); }
    }
    void Require(params RecordingState[] states)
    {
        if (!states.Contains(State)) throw new InvalidOperationException($"Comando indisponível no estado {State}.");
    }
    public Task StartAsync(CaptureArea area, string outputDirectory) => Locked(async () =>
    {
        Require(RecordingState.Idle);
        if (area.Width < 2 || area.Height < 2) throw new ArgumentOutOfRangeException(nameof(area), "A região deve ter pelo menos 2 x 2 pixels.");
        if (!File.Exists(ffmpegPath) || !File.Exists(ffprobePath)) throw new FileNotFoundException("FFmpeg e FFprobe são necessários. Confira a pasta components ou as configurações.");
        segments.Clear(); ownedFiles.Clear(); discardable = false; ownedDirectory = null; ownershipId = null; completed = TimeSpan.Zero; Interlocked.Exchange(ref segmentMicroseconds, 0);
        this.area = area; this.outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(this.outputDirectory);
        // CreateNew proves write access without touching existing files.
        var check = Path.Combine(this.outputDirectory, $".eagle-eyes-{Guid.NewGuid():N}.tmp");
        using (new FileStream(check, FileMode.CreateNew, FileAccess.Write)) { }
        File.Delete(check);
        RecoveryDirectory = Path.Combine(this.outputDirectory, $".eagle-eyes-session-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}");
        if (Directory.Exists(RecoveryDirectory)) throw new IOException("O diretório reservado para a sessão já existe.");
        Directory.CreateDirectory(RecoveryDirectory);
        ownedDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(RecoveryDirectory));
        ownershipId = Guid.NewGuid().ToString("N");
        discardable = true;
        try
        {
            var marker = OwnFile("session.json");
            await File.WriteAllTextAsync(marker, JsonSerializer.Serialize(new { OwnershipId = ownershipId, area, Started = DateTimeOffset.Now, Fps = 15, Audio = false }));
            await StartSegment(); State = RecordingState.Recording;
        }
        catch { State = RecordingState.Faulted; throw; }
    });
    public Task PauseAsync() => Locked(async () =>
    {
        Require(RecordingState.Recording);
        try { await EndSegment(); State = RecordingState.Paused; }
        catch { State = RecordingState.Faulted; throw; }
    });
    public Task ResumeAsync() => Locked(async () =>
    {
        Require(RecordingState.Paused);
        try { await StartSegment(); State = RecordingState.Recording; }
        catch { State = RecordingState.Faulted; throw; }
    });
    public async Task<string> StopAsync()
    {
        string? result = null;
        await Locked(async () =>
        {
            Require(RecordingState.Recording, RecordingState.Paused);
            var wasRecording = State == RecordingState.Recording;
            State = RecordingState.Finalizing;
            try
            {
                if (wasRecording) await EndSegment();
                var list = OwnFile("segments.txt");
                // Segment names are generated by us and contain no quote or user path.
                await File.WriteAllLinesAsync(list, segments.Select(p => $"file '{Path.GetFileName(p)}'"));
                var pending = OwnFile("final.mp4");
                await Run(ffmpegPath, ["-hide_banner", "-nostdin", "-n", "-f", "concat", "-safe", "1", "-i", list, "-map", "0:v:0", "-c", "copy", "-an", "-movflags", "+faststart", pending], TimeSpan.FromMinutes(5));
                completed = await Validate(pending);
                // Move with overwrite=false atomically reserves a unique final name.
                for (int i = 0; ; i++)
                {
                    var suffix = i == 0 ? "" : $"-{i}";
                    var candidate = Path.Combine(outputDirectory, $"EagleEyes-{DateTime.Now:yyyyMMdd-HHmmss}{suffix}.mp4");
                    try { File.Move(pending, candidate, false); result = candidate; break; }
                    catch (IOException) when (File.Exists(candidate)) { }
                }
                discardable = false;
                State = RecordingState.Idle;
                // Keep session segments deliberately for explicit recovery/inspection; never delete before verification.
            }
            catch { State = RecordingState.Faulted; throw; }
        });
        return result!;
    }
    string OwnFile(string name)
    {
        var path = Path.GetFullPath(Path.Combine(ownedDirectory!, name));
        if (!string.Equals(Path.GetDirectoryName(path), ownedDirectory, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Caminho de sessão inválido.");
        ownedFiles.Add(path);
        return path;
    }

    public async Task<DiscardResult> DiscardAsync()
    {
        DiscardResult? result = null;
        await Locked(async () =>
        {
            Require(RecordingState.Recording, RecordingState.Paused, RecordingState.Faulted);
            if (!discardable || ownedDirectory is null || ownershipId is null)
                throw new InvalidOperationException("Não existe sessão corrente para descartar.");
            State = RecordingState.Finalizing;
            if (!await EndCaptureForDiscard())
            {
                State = RecordingState.Faulted;
                result = new(false, [ownedDirectory]);
                return;
            }
            result = await Task.Run(DeleteOwnedSessionFiles);
            State = result.Complete ? RecordingState.Idle : RecordingState.Faulted;
            if (result.Complete)
            {
                discardable = false; completed = TimeSpan.Zero; Interlocked.Exchange(ref segmentMicroseconds, 0);
                segments.Clear(); ownedFiles.Clear(); RecoveryDirectory = null;
                ownedDirectory = null; ownershipId = null;
            }
        });
        return result!;
    }

    async Task<bool> EndCaptureForDiscard()
    {
        var process = capture;
        if (process is null) return true;
        try
        {
            if (!process.HasExited)
            {
                try
                {
                    using var graceful = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.StandardInput.WriteLineAsync("q".AsMemory(), graceful.Token);
                    await process.StandardInput.FlushAsync(graceful.Token);
                    await process.WaitForExitAsync(graceful.Token);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidOperationException)
                {
                    Trace.WriteLine($"Eagle Eyes discard graceful stop: {ex}");
                    if (!process.HasExited) process.Kill(true);
                    using var forced = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await process.WaitForExitAsync(forced.Token);
                }
            }
            await FinishCaptureReaders();
            process.Dispose(); capture = null; stderrTask = null; progressTask = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            Trace.WriteLine($"Eagle Eyes discard process shutdown: {ex}");
            // Never remove files while a process might still be writing to them.
            return false;
        }
    }

    static bool HasReparseAncestor(string path)
    {
        for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    bool OwnsSessionDirectory()
    {
        var directory = ownedDirectory!;
        if (!string.Equals(directory, Path.TrimEndingDirectorySeparator(Path.GetFullPath(RecoveryDirectory!)), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(directory), Path.TrimEndingDirectorySeparator(outputDirectory), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(directory).StartsWith(".eagle-eyes-session-", StringComparison.Ordinal) || HasReparseAncestor(directory)) return false;
        var marker = Path.Combine(directory, "session.json");
        if (!ownedFiles.Contains(marker) || (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0) return false;
        using var document = JsonDocument.Parse(File.ReadAllText(marker));
        return document.RootElement.TryGetProperty("OwnershipId", out var id) && id.GetString() == ownershipId;
    }

    DiscardResult DeleteOwnedSessionFiles()
    {
        var directory = ownedDirectory!;
        try
        {
            if (!OwnsSessionDirectory()) return new(false, [directory]);
            var marker = Path.Combine(directory, "session.json");
            foreach (var path in ownedFiles.Where(p => !string.Equals(p, marker, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    // Recheck the root and all ancestors before each nonrecursive deletion.
                    if (!OwnsSessionDirectory() || !string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), directory, StringComparison.OrdinalIgnoreCase))
                        return new(false, [directory]);
                    if (!File.Exists(path) && !Directory.Exists(path)) continue;
                    if ((File.GetAttributes(path) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) continue;
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { Trace.WriteLine($"Eagle Eyes discard file {path}: {ex}"); }
            }
            // Preserve marker on partial failure, so a later retry can still establish ownership.
            var remaining = Directory.GetFileSystemEntries(directory).Where(p => !string.Equals(p, marker, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (remaining.Length != 0) return new(false, remaining.Append(marker).ToArray());
            if (!OwnsSessionDirectory()) return new(false, [directory]);
            File.Delete(marker);
            Directory.Delete(directory, recursive: false);
            return new(true, []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine($"Eagle Eyes discard directory {directory}: {ex}");
            return new(false, [directory]);
        }
    }
    async Task StartSegment()
    {
        var a = area!;
        var path = OwnFile($"segment-{segments.Count:D4}.mp4");
        lock (logLock) errors.Clear();
        Interlocked.Exchange(ref segmentMicroseconds, 0);
        captureReaders?.Dispose();
        captureReaders = new CancellationTokenSource();
        capture = Process.Start(Info(ffmpegPath, ["-hide_banner", "-progress", "pipe:1", "-stats_period", "0.2", "-nostats", "-n", "-f", "gdigrab", "-framerate", "15", "-draw_mouse", "1", "-offset_x", a.X.ToString(CultureInfo.InvariantCulture), "-offset_y", a.Y.ToString(CultureInfo.InvariantCulture), "-video_size", $"{a.Width}x{a.Height}", "-i", "desktop", "-map", "0:v:0", "-an", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-pix_fmt", "yuv420p", "-r", "15", path])) ?? throw new IOException("Não foi possível iniciar FFmpeg.");
        stderrTask = Drain(capture.StandardError, errors, captureReaders.Token);
        progressTask = ReadProgress(capture.StandardOutput, Interlocked.Increment(ref captureGeneration), captureReaders.Token);
        await Task.Delay(500);
        if (capture.HasExited)
        {
            await stderrTask;
            throw new IOException($"FFmpeg não iniciou a captura. {Log()}");
        }
        segments.Add(path);
    }
    async Task EndSegment()
    {
        var p = capture ?? throw new InvalidOperationException("Nenhuma captura ativa.");
        try
        {
            if (!p.HasExited) { await p.StandardInput.WriteLineAsync("q"); await p.StandardInput.FlushAsync(); }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await p.WaitForExitAsync(timeout.Token);
            await FinishCaptureReaders();
            if (p.ExitCode != 0) throw new IOException($"FFmpeg falhou ao encerrar trecho. {Log()}");
            completed += await Validate(segments[^1]);
        }
        catch (OperationCanceledException)
        {
            if (!p.HasExited)
            {
                p.Kill(true);
                using var terminated = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await p.WaitForExitAsync(terminated.Token);
            }
            throw new IOException("FFmpeg não respondeu ao finalizar o trecho; os temporários foram preservados.");
        }
        finally
        {
            Interlocked.Exchange(ref segmentMicroseconds, 0);
            // Keep the process handle on failed shutdown so DiscardAsync can still stop it.
            if (p.HasExited) { p.Dispose(); capture = null; }
        }
    }
    async Task<TimeSpan> Validate(string path)
    {
        var json = await Run(ffprobePath, ["-v", "error", "-show_streams", "-show_format", "-of", "json", path], TimeSpan.FromSeconds(30));
        using var doc = JsonDocument.Parse(json);
        var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        if (streams.Length != 1 || streams[0].GetProperty("codec_type").GetString() != "video" || new FileInfo(path).Length == 0)
            throw new IOException("O arquivo não contém somente um fluxo de vídeo válido.");
        if (!doc.RootElement.GetProperty("format").TryGetProperty("duration", out var duration) || !double.TryParse(duration.GetString(), CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
            throw new IOException("O arquivo não contém duração de vídeo válida.");
        return TimeSpan.FromSeconds(seconds);
    }
    static ProcessStartInfo Info(string exe, IEnumerable<string> args)
    {
        var info = new ProcessStartInfo(exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        return info;
    }
    async Task<string> Run(string exe, IEnumerable<string> args, TimeSpan limit)
    {
        using var p = Process.Start(Info(exe, args)) ?? throw new IOException("Não foi possível executar ferramenta de vídeo.");
        var error = new StringBuilder(); var err = Drain(p.StandardError, error); var stdout = p.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(limit);
        try { await p.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!p.HasExited) p.Kill(true); throw new IOException("Tempo limite na ferramenta de vídeo; temporários preservados."); }
        await err;
        if (p.ExitCode != 0) throw new IOException($"Falha no processamento de vídeo: {error}");
        return await stdout;
    }
    async Task ReadProgress(StreamReader reader, long generation, CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (line.StartsWith("out_time_us=", StringComparison.Ordinal) &&
                    long.TryParse(line.AsSpan("out_time_us=".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds) &&
                    microseconds >= 0 && microseconds <= TimeSpan.MaxValue.Ticks / 10 &&
                    generation == Interlocked.Read(ref captureGeneration) && !cancellationToken.IsCancellationRequested)
                    Interlocked.Exchange(ref segmentMicroseconds, microseconds);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        { Trace.WriteLine($"Eagle Eyes capture progress reader: {ex.Message}"); }
    }
    async Task FinishCaptureReaders()
    {
        var readers = Task.WhenAll(stderrTask ?? Task.CompletedTask, progressTask ?? Task.CompletedTask);
        try { await readers.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (TimeoutException)
        {
            captureReaders?.Cancel();
            // Both stream readers use cancellation-aware operations and handle cancellation locally.
            try { await readers.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (TimeoutException) { Trace.WriteLine("Eagle Eyes stream readers did not finish within shutdown limit."); }
        }
    }
    async Task Drain(StreamReader reader, StringBuilder target, CancellationToken cancellationToken = default)
    {
        try
        {
            var buffer = new char[2048]; int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                lock (logLock) { target.Append(buffer, 0, read); if (target.Length > 16000) target.Remove(0, target.Length - 16000); }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
        { Trace.WriteLine($"Eagle Eyes capture error reader: {ex.Message}"); }
    }
    string Log() { lock (logLock) return errors.ToString(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        captureReaders?.Cancel();
        Interlocked.Increment(ref captureGeneration);
        if (capture != null) { try { if (!capture.HasExited) capture.Kill(true); } catch (InvalidOperationException) { } capture.Dispose(); }
        // Do not remove potentially recoverable files on dispose.
    }
}
