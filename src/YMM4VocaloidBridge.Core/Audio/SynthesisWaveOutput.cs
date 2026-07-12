namespace YMM4VocaloidBridge.Core.Audio;

public sealed record SynthesisWaveOutput(string RequestedPath, string RenderPath)
{
    public static SynthesisWaveOutput Create(string requestedPath, string workDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDirectory);

        var requested = Path.GetFullPath(requestedPath);
        var render = string.Equals(Path.GetExtension(requested), ".wav", StringComparison.OrdinalIgnoreCase)
            ? requested
            : Path.Combine(Path.GetFullPath(workDirectory), "render.wav");
        return new SynthesisWaveOutput(requested, render);
    }

    public async Task PublishAsync(CancellationToken cancellationToken = default)
    {
        if (string.Equals(RequestedPath, RenderPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(RequestedPath)!);
        await using var input = File.Open(RenderPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var output = File.Open(RequestedPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
    }
}
