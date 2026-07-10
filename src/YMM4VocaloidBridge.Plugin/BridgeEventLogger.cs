using System.IO;
using System.Text;
using System.Text.Json;

namespace YMM4VocaloidBridge.Plugin;

internal sealed class BridgeEventLogger
{
    private readonly string logPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YMM4VocaloidBridge",
        "logs",
        "bridge.jsonl");

    public async Task WriteAsync(string eventName, object? details = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var line = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            eventName,
            details,
        });
        await File.AppendAllTextAsync(logPath, line + Environment.NewLine, new UTF8Encoding(false)).ConfigureAwait(false);
    }
}
