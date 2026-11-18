using System.Text.Json;
using EagleEyes.App;
int passed = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); passed++; }
string Feed(string tag, bool draft = false, bool preview = false) => JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease = preview, html_url = "https://malicious.invalid/installer.exe" });
var current = new Version(1, 2, 0, 0);
Check(UpdateService.Parse(Feed("v1.2.0"), current) == null, "three/four-part versions equal");
Check(UpdateService.Parse(Feed("v1.1.99"), current) == null, "no downgrade");
Check(UpdateService.Parse(Feed("v1.3.0", draft: true), current) == null, "draft ignored");
Check(UpdateService.Parse(Feed("v1.3.0", preview: true), current) == null, "preview ignored");
Check(UpdateService.Parse(Feed("v1.3.0-beta.1"), current) == null, "preview tag ignored");
foreach (var bad in new[] { "1.3", "v1.3.0.0", "v1.3.-1", "v1.3.0/evil", "v1.3.0?x=1", "v999999999999.0.0", "v1.3. 0", "vv1.3.0", "" }) Check(UpdateService.Parse(Feed(bad), current) == null, "reject tag " + bad);
var newer = UpdateService.Parse(Feed("v1.10.0"), current)!;
Check(newer.Version == new Version(1, 10, 0), "numeric version comparison");
Check(newer.Page.AbsoluteUri == "https://github.com/OVinicius1995/Eagle-Eyes/releases/tag/v1.10.0", "fixed official repository; ignores response URL");
Check(UpdateService.Parse(Feed("2.0.0"), current)!.Page.AbsoluteUri.EndsWith("/tag/2.0.0"), "tag without v retains original route");
bool invalid = false; try { UpdateService.Parse("{bad", current); } catch (JsonException) { invalid = true; } Check(invalid, "malformed JSON fails closed");
invalid = false; try { UpdateService.Parse("{}", current); } catch (KeyNotFoundException) { invalid = true; } Check(invalid, "missing fields fails closed");
if (args.Contains("--live"))
{
    var release = await UpdateService.CheckAsync(new Version(0, 0, 0));
    Check(release != null && release.Page.Host == "github.com", "live GitHub release check");
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    bool cancelled = false; try { await UpdateService.CheckAsync(current, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "cancelled network operation");
}
Console.WriteLine($"{passed} update checks passed.");
