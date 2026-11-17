using System.Diagnostics;
using System.Text.Json;
using EagleEyes.Core;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var binaries = Path.Combine(root, "components", "ffmpeg-master-latest-win64-gpl", "bin");
var ffmpeg = Path.Combine(binaries, "ffmpeg.exe");
var ffprobe = Path.Combine(binaries, "ffprobe.exe");
var artifacts = Path.Combine(root, ".test-artifacts", "qa-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(artifacts);
var count = 0;
void Assert(bool value, string message) { if (!value) throw new Exception(message); count++; Console.WriteLine("PASS " + message); }
async Task Reject(Func<Task> action, string message)
{
    try { await action(); } catch { Assert(true, message); return; }
    throw new Exception("Expected rejection: " + message);
}
using (var session = new RecordingSession(ffmpeg, ffprobe))
{
    Assert(session.State == RecordingState.Idle, "New session is Idle");
    Assert(session.RecordedDuration == TimeSpan.Zero, "New session duration is zero");
    await Reject(() => session.PauseAsync(), "Cannot pause before start");
    await Reject(() => session.ResumeAsync(), "Cannot resume before start");
    await Reject(async () => { await session.StopAsync(); }, "Cannot finalize before start");
    await Reject(() => session.StartAsync(new CaptureArea(0, 0, 0, 100), artifacts), "Reject zero width");
}
await Reject(async () => { using var missing = new RecordingSession(Path.Combine(artifacts, "absent.exe"), ffprobe); await missing.StartAsync(new CaptureArea(0, 0, 100, 100), artifacts); }, "Report absent FFmpeg");

var blockedOutput = Path.Combine(artifacts, "not-a-directory");
File.WriteAllText(blockedOutput, "Output path must be a directory");
using (var blocked = new RecordingSession(ffmpeg, ffprobe))
    await Reject(() => blocked.StartAsync(new CaptureArea(0, 0, 100, 100), blockedOutput), "Reject output path that is a file");
if (args.Contains("--live"))
{
    using var session = new RecordingSession(ffmpeg, ffprobe);
    var wall = Stopwatch.StartNew();
    await session.StartAsync(new CaptureArea(0, 0, 640, 360), artifacts);
    Assert(session.State == RecordingState.Recording, "Live capture started");
    await Reject(() => session.StartAsync(new CaptureArea(0, 0, 640, 360), artifacts), "Reject duplicate recording");
    await Task.Delay(1800);
    await session.PauseAsync();
    var pausedDuration = session.RecordedDuration;
    await Task.Delay(1500);
    Assert(Math.Abs((session.RecordedDuration - pausedDuration).TotalSeconds) < .1, "Duration frozen during first pause");
    await session.ResumeAsync();
    await Task.Delay(1800);
    await session.PauseAsync();
    pausedDuration = session.RecordedDuration;
    await Task.Delay(1500);
    Assert(Math.Abs((session.RecordedDuration - pausedDuration).TotalSeconds) < .1, "Duration frozen during second pause");
    await session.ResumeAsync();
    await Task.Delay(1800);
    var output = await session.StopAsync();
    Assert(File.Exists(output) && new FileInfo(output).Length > 1000, "Final MP4 exists after two pauses");
    var start = new ProcessStartInfo(ffprobe) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    foreach (var arg in new[] { "-v", "error", "-show_streams", "-show_format", "-of", "json", output }) start.ArgumentList.Add(arg);
    using var probe = Process.Start(start)!;
    var json = await probe.StandardOutput.ReadToEndAsync();
    var error = await probe.StandardError.ReadToEndAsync();
    await probe.WaitForExitAsync();
    Assert(probe.ExitCode == 0, "ffprobe parses final MP4: " + error);
    using var doc = JsonDocument.Parse(json);
    var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
    Assert(streams.Count(s => s.GetProperty("codec_type").GetString() == "video") == 1, "Exactly one video stream");
    Assert(streams.All(s => s.GetProperty("codec_type").GetString() != "audio"), "No audio streams");
    var video = streams.Single(s => s.GetProperty("codec_type").GetString() == "video");
    Assert(video.GetProperty("width").GetInt32() == 640 && video.GetProperty("height").GetInt32() == 360, "Region dimensions preserved");
    Assert(video.GetProperty("r_frame_rate").GetString() == "15/1", "15 fps");
    var duration = double.Parse(doc.RootElement.GetProperty("format").GetProperty("duration").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
    Assert(duration > 3 && duration < wall.Elapsed.TotalSeconds - 2, "MP4 excludes pause time");
    File.WriteAllText(Path.Combine(artifacts, "probe.json"), json);
    Console.WriteLine("ARTIFACT " + output);
    var originalBytes = File.ReadAllBytes(output);
    await session.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    Assert(session.RecordedDuration.TotalSeconds < 2, "Reused session resets recorded duration");
    await Task.Delay(1200);
    await session.PauseAsync();
    var secondOutput = await session.StopAsync();
    Assert(secondOutput != output, "Second recording receives a unique final filename");
    Assert(File.ReadAllBytes(output).SequenceEqual(originalBytes), "First MP4 remains unchanged");
    start.ArgumentList[start.ArgumentList.Count - 1] = secondOutput;
    using var secondProbe = Process.Start(start)!;
    var secondJson = await secondProbe.StandardOutput.ReadToEndAsync();
    await secondProbe.StandardError.ReadToEndAsync();
    await secondProbe.WaitForExitAsync();
    using var secondDoc = JsonDocument.Parse(secondJson);
    var secondVideo = secondDoc.RootElement.GetProperty("streams")[0];
    Assert(secondVideo.GetProperty("width").GetInt32() == 320 && secondVideo.GetProperty("height").GetInt32() == 240, "Second recording does not concatenate first session segments");
    Assert(session.RecordedDuration.TotalSeconds < 4, "Second duration excludes previous recording");
}
if (args.Contains("--faults"))
{
    var disposableProbe = Path.Combine(artifacts, "ffprobe-for-failure.exe");
    File.Copy(ffprobe, disposableProbe);
    using var brokenFinalization = new RecordingSession(ffmpeg, disposableProbe);
    await brokenFinalization.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await Task.Delay(1500);
    await brokenFinalization.PauseAsync();
    File.Move(disposableProbe, disposableProbe + ".unavailable");
    await Reject(async () => { await brokenFinalization.StopAsync(); }, "Report probe failure at finalization");
    var recovery = brokenFinalization.RecoveryDirectory;
    Assert(!string.IsNullOrEmpty(recovery) && Directory.Exists(recovery), "Preserve recovery directory after failed finalization");
    Assert(Directory.GetFiles(recovery!, "*", SearchOption.AllDirectories).Any(p => new FileInfo(p).Length > 0), "Preserve nonempty temporary evidence after failure");
    Console.WriteLine("RECOVERY " + recovery);
}
Console.WriteLine($"Passed {count} assertions. Live capture {(args.Contains("--live") ? "executed" : "NOT executed; pass --live on an interactive Windows desktop")}. Artifacts: {artifacts}");
