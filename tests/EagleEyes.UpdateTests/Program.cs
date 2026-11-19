using System.Text.Json;
using EagleEyes.App;
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
string Feed(string tag, bool draft = false, bool preview = false) => JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease = preview, assets = new[] { new { name = $"EagleEyes-{tag.TrimStart('v')}-win-x64-Setup.exe", size = 3, digest = "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3 })), browser_download_url = "https://malicious.invalid/installer.exe" } } });
var current = new Version(1, 2, 0, 0);
Check(UpdateService.Parse(Feed("v1.2.0"), current) == null, "three/four-part versions equal");
Check(UpdateService.Parse(Feed("v1.1.99"), current) == null, "no downgrade");
Check(UpdateService.Parse(Feed("v1.3.0", draft: true), current) == null, "draft ignored");
Check(UpdateService.Parse(Feed("v1.3.0", preview: true), current) == null, "preview ignored");
Check(UpdateService.Parse(Feed("v1.3.0-beta.1"), current) == null, "preview tag ignored");
foreach (var bad in new[] { "1.3", "v1.3.0.0", "v1.3.-1", "v1.3.0/evil", "v1.3.0?x=1", "v999999999999.0.0", "v1.3. 0", "vv1.3.0", "" }) Check(UpdateService.Parse(Feed(bad), current) == null, "reject tag " + bad);
var newer = UpdateService.Parse(Feed("v1.10.0"), current)!;
Check(newer.Version == new Version(1, 10, 0), "numeric version comparison");
Check(newer.Installer.AbsoluteUri == "https://github.com/OVinicius1995/Eagle-Eyes/releases/download/v1.10.0/EagleEyes-1.10.0-win-x64-Setup.exe", "fixed official repository; ignores response URL");
Check(UpdateService.Parse(Feed("2.0.0"), current)!.Installer.AbsoluteUri.EndsWith("/download/2.0.0/EagleEyes-2.0.0-win-x64-Setup.exe"), "tag without v retains original route");
bool invalid = false; try { UpdateService.Parse("{bad", current); } catch (JsonException) { invalid = true; } Check(invalid, "malformed JSON fails closed");
invalid = false; try { UpdateService.Parse("{}", current); } catch (KeyNotFoundException) { invalid = true; } Check(invalid, "missing fields fails closed");
foreach (byte[] bytes in new byte[][] { [1, 2], [1, 2, 3, 4], [1, 2, 4], [1, 2, 3] })
{
    using var output = new MemoryStream();
    bool accepted = true;
    try { await UpdateService.CopyVerifiedAsync(new MemoryStream(bytes), output, newer, CancellationToken.None); }
    catch (InvalidDataException) { accepted = false; }
    Check(accepted == bytes.SequenceEqual(new byte[] { 1, 2, 3 }), "validate payload " + Convert.ToHexString(bytes));
}
string goodFeed = Feed("v1.10.0");
foreach (var badFeed in new[]
{
    goodFeed.Replace("sha256:", "md5:"), goodFeed.Replace("\"size\":3", "\"size\":0"),
    goodFeed.Replace("\"size\":3", "\"size\":268435457"), goodFeed.Replace("Setup.exe", "portable.zip")
})
{
    bool rejected = false;
    try { UpdateService.Parse(badFeed, current); } catch (InvalidDataException) { rejected = true; }
    Check(rejected, "reject incomplete or invalid installer metadata");
}
using (var cancelCopy = new CancellationTokenSource())
{
    cancelCopy.Cancel();
    bool cancelled = false;
    try { await UpdateService.CopyVerifiedAsync(new MemoryStream([1, 2, 3]), Stream.Null, newer, cancelCopy.Token); }
    catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "cancel installer transfer before handoff");
}
if (args.Contains("--live"))
{
    var release = await UpdateService.CheckAsync(new Version(0, 0, 0));
    Check(release != null && release.Installer.Host == "github.com", "live GitHub release check");
    using var installer = await UpdateService.DownloadAsync(release!, CancellationToken.None);
    Check(installer.Length == release!.Size, "download and verify public installer");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    bool cancelled = false; try { await UpdateService.CheckAsync(current, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "cancelled network operation");
}
Console.WriteLine($"{passed} update checks passed.");
