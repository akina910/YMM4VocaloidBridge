using YMM4VocaloidBridge.Core;

namespace YMM4VocaloidBridge.Automation;

public sealed record VocaloidRenderRequest(
    SynthesisArtifacts Artifacts,
    BridgeOptions Options,
    string OutputWavePath,
    VocaloidInstallation Installation);

public sealed record VocaloidRenderResult(
    string OutputWavePath,
    string DriverName,
    bool UsedFallback,
    IReadOnlyList<string> Events);

public interface IVocaloidDriver
{
    Task<VocaloidRenderResult> RenderAsync(VocaloidRenderRequest request, CancellationToken cancellationToken = default);
}

public sealed class VocaloidAutomationException(string message, Exception? innerException = null)
    : Exception(message, innerException);
