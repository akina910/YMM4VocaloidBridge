using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace YMM4VocaloidBridge.Core.Caching;

public static class SynthesisCacheKey
{
    private const string SchemaVersion = "ymm4-vocaloid-bridge-cache-v1";

    public static string Create(string normalizedText, BridgeOptions options, string vocaloidVersion)
    {
        var payload = JsonSerializer.Serialize(new { SchemaVersion, normalizedText, options, vocaloidVersion });
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }
}

public sealed class SynthesisCache(string rootDirectory)
{
    public async Task<bool> TryRestoreAsync(string key, string destinationPath, CancellationToken cancellationToken = default)
    {
        var source = GetPath(key);
        if (!File.Exists(source))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destinationPath))!);
        await CopyAsync(source, destinationPath, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task StoreAsync(string key, string sourcePath, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(rootDirectory);
        var destination = GetPath(key);
        var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        await CopyAsync(sourcePath, temporary, cancellationToken).ConfigureAwait(false);
        File.Move(temporary, destination, overwrite: true);
    }

    private string GetPath(string key) => Path.Combine(rootDirectory, key + ".wav");

    private static async Task CopyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        await using var input = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var output = File.Open(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }
}
