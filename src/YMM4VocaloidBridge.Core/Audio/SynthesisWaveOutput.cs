using YMM4VocaloidBridge.Core.IO;

namespace YMM4VocaloidBridge.Core.Audio;

public sealed class SynthesisWaveOutput
{
    private SynthesisWaveOutput(string requestedPath, string renderPath)
    {
        RequestedPath = requestedPath;
        RenderPath = renderPath;
    }

    public string RequestedPath { get; }

    public string RenderPath { get; }

    public static SynthesisWaveOutput Create(string requestedPath, string workDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workDirectory);

        var requested = Path.GetFullPath(requestedPath);
        var work = Path.GetFullPath(workDirectory);
        var render = Path.Combine(work, "render.wav");
        if (string.Equals(requested, render, StringComparison.OrdinalIgnoreCase))
        {
            render = Path.Combine(work, "render-source.wav");
        }

        return new SynthesisWaveOutput(requested, render);
    }

    public async Task PublishAsync(CancellationToken cancellationToken = default)
    {
        await AtomicFilePublisher.CopyAsync(RenderPath, RequestedPath, cancellationToken).ConfigureAwait(false);
    }
}
