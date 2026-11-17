using System.Diagnostics;
using System.Text.Json;
using EagleEyes.Core;

// Internal fake capture process for the forced-shutdown test; never records pixels.
if (args.Contains("gdigrab")) { await Task.Delay(Timeout.Infinite); return; }

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
    await Reject(async () => { await session.DiscardAsync(); }, "Cannot discard before start");
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
    // Discard tests operate only on their own generated artifact directory.
    var neighbor = Path.Combine(artifacts, ".eagle-eyes-session-other-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(neighbor);
    var neighborSentinel = Path.Combine(neighbor, "segment-0000.mp4");
    File.WriteAllText(neighborSentinel, "Other session must survive");
    var finalsBeforeDiscard = Directory.GetFiles(artifacts, "*.mp4").Order().ToArray();
    using var discarded = new RecordingSession(ffmpeg, ffprobe);
    var starting = discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await Reject(async () => { await discarded.DiscardAsync(); }, "Reject discard during concurrent start");
    await starting;
    var recordingDirectory = discarded.RecoveryDirectory!;
    var discarding = discarded.DiscardAsync();
    await Reject(async () => { await discarded.DiscardAsync(); }, "Reject duplicate concurrent discard");
    var discardResult = await discarding;
    Assert(discardResult.Complete && discardResult.RemainingPaths.Count == 0, "Discard active recording completes");
    Assert(discarded.State == RecordingState.Idle && !Directory.Exists(recordingDirectory), "Discard stops capture and removes its own session directory");
    Assert(discarded.RecordedDuration == TimeSpan.Zero, "Discard resets recorded duration");
    Assert(Directory.GetFiles(artifacts, "*.mp4").Order().SequenceEqual(finalsBeforeDiscard), "Discard does not create final MP4");
    Assert(File.ReadAllBytes(output).SequenceEqual(originalBytes), "Discard preserves previously finalized MP4");
    Assert(File.ReadAllText(neighborSentinel) == "Other session must survive", "Discard preserves another session");
    await Reject(async () => { await discarded.DiscardAsync(); }, "Reject discard of completed discard");

    await discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await discarded.PauseAsync();
    var pausedDirectory = discarded.RecoveryDirectory!;
    discardResult = await discarded.DiscardAsync();
    Assert(discardResult.Complete && !Directory.Exists(pausedDirectory), "Discard paused session without publishing");
    Assert(Directory.GetFiles(artifacts, "*.mp4").Order().SequenceEqual(finalsBeforeDiscard), "Paused discard creates no final MP4");

    await discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    var partialDirectory = discarded.RecoveryDirectory!;
    var unknown = Path.Combine(partialDirectory, "unknown-sentinel.txt");
    File.WriteAllText(unknown, "Do not erase unknown content");
    discardResult = await discarded.DiscardAsync();
    Assert(!discardResult.Complete && discarded.State == RecordingState.Faulted, "Unknown file produces partial discard and Faulted state");
    Assert(discardResult.RemainingPaths.Contains(unknown) && File.ReadAllText(unknown) == "Do not erase unknown content", "Unknown file is preserved and reported");
    Assert(!Directory.GetFiles(partialDirectory, "segment-*.mp4").Any(), "Partial discard still removes known segments after stopping capture");
    // Explicitly remove only this test's generated sentinel, then retry the same discard.
    File.Delete(unknown);
    discardResult = await discarded.DiscardAsync();
    Assert(discardResult.Complete && !Directory.Exists(partialDirectory), "Partial discard can be retried after unknown file removal");

    await discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await discarded.PauseAsync();
    var tamperedDirectory = discarded.RecoveryDirectory!;
    var markerPath = Path.Combine(tamperedDirectory, "session.json");
    var markerBytes = File.ReadAllBytes(markerPath);
    File.WriteAllText(markerPath, "{\"OwnershipId\":\"different-session\"}");
    discardResult = await discarded.DiscardAsync();
    Assert(!discardResult.Complete && Directory.GetFiles(tamperedDirectory, "segment-*.mp4").Any(), "Ownership mismatch preserves all session contents");
    File.WriteAllBytes(markerPath, markerBytes);
    Assert((await discarded.DiscardAsync()).Complete, "Restored ownership permits discard retry");

    await discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await discarded.PauseAsync();
    var linkedDirectory = discarded.RecoveryDirectory!;
    var linkedSegment = Directory.GetFiles(linkedDirectory, "segment-*.mp4").Single();
    File.Delete(linkedSegment);
    bool linkCreated = false;
    try { File.CreateSymbolicLink(linkedSegment, neighborSentinel); linkCreated = true; }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    { Console.WriteLine("SKIP file reparse test: Windows did not permit symbolic link creation: " + ex.Message); }
    if (linkCreated)
    {
        discardResult = await discarded.DiscardAsync();
        Assert(!discardResult.Complete && discardResult.RemainingPaths.Contains(linkedSegment), "Known filename replaced by symbolic link is preserved and reported");
        Assert(File.ReadAllText(neighborSentinel) == "Other session must survive", "Discard never follows file symbolic link to another session");
        File.Delete(linkedSegment); // Remove only the generated test link, never its target.
    }
    Assert((await discarded.DiscardAsync()).Complete, "Discard completes after generated file link removal");

    await discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await discarded.PauseAsync();
    linkedDirectory = discarded.RecoveryDirectory!;
    var heldDirectory = linkedDirectory + "-held";
    Directory.Move(linkedDirectory, heldDirectory);
    linkCreated = false;
    try { Directory.CreateSymbolicLink(linkedDirectory, neighbor); linkCreated = true; }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    { Console.WriteLine("SKIP directory reparse test: Windows did not permit symbolic link creation: " + ex.Message); }
    if (linkCreated)
    {
        discardResult = await discarded.DiscardAsync();
        Assert(!discardResult.Complete && discardResult.RemainingPaths.Contains(linkedDirectory), "Session directory replaced by symbolic link is rejected");
        Assert(File.ReadAllText(neighborSentinel) == "Other session must survive", "Discard never follows directory symbolic link to another session");
        Directory.Delete(linkedDirectory, recursive: false); // Remove this test's link only.
    }
    Directory.Move(heldDirectory, linkedDirectory);
    Assert((await discarded.DiscardAsync()).Complete, "Restored original directory permits safe retry");
    await discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await discarded.PauseAsync();
    var lockedDirectory = discarded.RecoveryDirectory!;
    var lockedSegment = Directory.GetFiles(lockedDirectory, "segment-*.mp4").Single();
    using (var held = new FileStream(lockedSegment, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        discardResult = await discarded.DiscardAsync();
        Assert(!discardResult.Complete && discardResult.RemainingPaths.Contains(lockedSegment), "Locked known segment produces partial result and reports remaining file");
        Assert(discarded.State == RecordingState.Faulted && File.Exists(lockedSegment), "Deletion failure preserves remaining segment in Faulted state");
    }
    Assert((await discarded.DiscardAsync()).Complete, "Discard retries successfully after file lock released");

    await discarded.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
    await Task.Delay(600);
    var reusedAfterDiscard = await discarded.StopAsync();
    Assert(File.Exists(reusedAfterDiscard) && discarded.State == RecordingState.Idle, "Session can record and finalize again after discard");
    await Reject(async () => { await discarded.DiscardAsync(); }, "Cannot discard an already published session");
    Assert(File.ReadAllBytes(output).SequenceEqual(originalBytes) && File.Exists(reusedAfterDiscard), "Rejected discard preserves all published videos");
}
if (args.Contains("--faults"))
{
    var harnessExecutable = Path.Combine(AppContext.BaseDirectory, "EagleEyes.Tests.exe");
    using (var unresponsive = new RecordingSession(harnessExecutable, ffprobe))
    {
        var shutdownWall = Stopwatch.StartNew();
        await unresponsive.StartAsync(new CaptureArea(0, 0, 320, 240), artifacts);
        var unresponsiveDirectory = unresponsive.RecoveryDirectory!;
        var forcedDiscard = await unresponsive.DiscardAsync();
        Assert(forcedDiscard.Complete && unresponsive.State == RecordingState.Idle && !Directory.Exists(unresponsiveDirectory), "Discard kills and awaits unresponsive fake capture without validating nonexistent video");
        Assert(shutdownWall.Elapsed.TotalSeconds >= 5 && shutdownWall.Elapsed.TotalSeconds < 20, "Unresponsive capture uses bounded graceful timeout then forced shutdown");
    }
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
    var failureDiscard = await brokenFinalization.DiscardAsync();
    Assert(failureDiscard.Complete && brokenFinalization.State == RecordingState.Idle && !Directory.Exists(recovery), "Discard Faulted session without invoking unavailable FFprobe");
}
Console.WriteLine($"Passed {count} assertions. Live capture {(args.Contains("--live") ? "executed" : "NOT executed; pass --live on an interactive Windows desktop")}. Artifacts: {artifacts}");
