using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using YMM4VocaloidBridge.Core.IO;

namespace YMM4VocaloidBridge.Core.Caching;

public static class SynthesisCacheKey
{
    private const string SchemaVersion = "ymm4-vocaloid-bridge-cache-v5-robot-speech";

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

        try
        {
            await AtomicFilePublisher.CopyAsync(source, destinationPath, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    public async Task StoreAsync(string key, string sourcePath, CancellationToken cancellationToken = default)
    {
        var destination = GetPath(key);
        await AtomicFilePublisher.CopyAsync(sourcePath, destination, cancellationToken).ConfigureAwait(false);
    }

    public void Remove(string key)
    {
        var path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string GetPath(string key) => Path.Combine(rootDirectory, key + ".wav");

}
