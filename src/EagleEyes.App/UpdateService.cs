using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
namespace EagleEyes.App;
internal sealed record UpdateRelease(Version Version, Uri Page);
internal static class UpdateService
{
    internal const string Repository = "OVinicius1995/Eagle-Eyes";
    static readonly HttpClient Client = CreateClient();
    static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("EagleEyes/1.2");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
    internal static Version CurrentVersion => typeof(UpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0);
    internal static async Task<UpdateRelease?> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        cancellationToken = timeout.Token;
        using var response = await Client.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 1_048_576) throw new InvalidDataException("Release response too large.");
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int count;
        while ((count = await source.ReadAsync(block, cancellationToken)) > 0)
        {
            if (buffer.Length + count > 1_048_576) throw new InvalidDataException("Release response too large.");
            buffer.Write(block, 0, count);
        }
        return Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()), current);
    }
    internal static UpdateRelease? Parse(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        var numeric = tag.StartsWith('v') ? tag[1..] : tag;
        var parts = numeric.Split('.');
        if (parts.Length != 3 || parts.Any(p => p.Length == 0 || p.Any(c => c < '0' || c > '9')) || !Version.TryParse(numeric, out var version)) return null;
        if (Normalize(version) <= Normalize(current)) return null;
        return new UpdateRelease(version, new Uri($"https://github.com/{Repository}/releases/tag/{Uri.EscapeDataString(tag)}"));
    }
    static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
}
