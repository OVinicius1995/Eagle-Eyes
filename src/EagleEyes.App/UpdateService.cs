using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
namespace EagleEyes.App;
internal sealed record UpdateRelease(Version Version, Uri Installer, long Size, string Sha256);
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
        var filename = $"EagleEyes-{version}-win-x64-Setup.exe";
        var asset = root.GetProperty("assets").EnumerateArray().SingleOrDefault(a => a.GetProperty("name").GetString() == filename);
        if (asset.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Instalador ausente na versão publicada.");
        long size = asset.GetProperty("size").GetInt64();
        var digest = asset.GetProperty("digest").GetString() ?? "";
        if (size <= 0 || size > 256L * 1024 * 1024 || !digest.StartsWith("sha256:", StringComparison.Ordinal) ||
            digest.Length != 71 || !digest[7..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Tamanho ou SHA-256 do instalador inválido.");
        return new UpdateRelease(version, new Uri($"https://github.com/{Repository}/releases/download/{tag}/{filename}"), size, digest[7..]);
    }

    internal static async Task<FileStream> DownloadAsync(UpdateRelease release, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(10));
        var uri = release.Installer;
        for (int redirects = 0; redirects <= 3; redirects++)
        {
            using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if ((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location = response.Headers.Location;
                if (location == null || !Uri.TryCreate(uri, location, out var next) ||
                    next.Scheme != "https" || next.Host != "release-assets.githubusercontent.com" ||
                    !next.IsDefaultPort || next.UserInfo.Length != 0)
                    throw new InvalidDataException("Destino de download inesperado.");
                uri = next;
                continue;
            }
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long length && length != release.Size)
                throw new InvalidDataException("Tamanho inesperado do instalador.");
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            var directory = Path.Combine(Path.GetTempPath(), "EagleEyes-Updates");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"EagleEyes-{release.Version}-Setup.exe");
            var partial = Path.Combine(directory, $"{Guid.NewGuid():N}.partial");
            try
            {
                await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                    await CopyVerifiedAsync(source, file, release, timeout.Token);
                File.Move(partial, path, true);
                var verified = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                try
                {
                    var digest = await System.Security.Cryptography.SHA256.HashDataAsync(verified, timeout.Token);
                    if (!Convert.ToHexString(digest).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("O instalador em disco foi alterado.");
                    return verified;
                }
                catch { verified.Dispose(); throw; }
            }
            finally { File.Delete(partial); }
        }
        throw new InvalidDataException("Excesso de redirecionamentos.");
    }

    internal static async Task CopyVerifiedAsync(Stream source, Stream output, UpdateRelease release, CancellationToken token)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var block = new byte[81920];
        long total = 0;
        int count;
        while ((count = await source.ReadAsync(block, token).ConfigureAwait(false)) > 0)
        {
            total += count;
            if (total > release.Size) throw new InvalidDataException("Instalador maior que o esperado.");
            hash.AppendData(block, 0, count);
            await output.WriteAsync(block.AsMemory(0, count), token).ConfigureAwait(false);
        }
        if (total != release.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A verificação do instalador falhou.");
    }
    static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(0, version.Build));
}
