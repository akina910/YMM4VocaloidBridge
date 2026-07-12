using System.Diagnostics;
using System.Text;

namespace YMM4VocaloidBridge.Automation;

public sealed class AssistedVocaloidDriver(FileReadyWaiter fileWaiter) : IVocaloidDriver
{
    public async Task<VocaloidRenderResult> RenderAsync(
        VocaloidRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputWavePath))!);
        if (File.Exists(request.OutputWavePath))
        {
            File.Delete(request.OutputWavePath);
        }

        var guidePath = Path.ChangeExtension(request.Artifacts.MidiPath, ".instructions.txt");
        var guide = $"""
            YMM4 Vocaloid Bridge assisted render

            1. VOCALOID6 Editor: File > Import > MIDI
               {request.Artifacts.MidiPath}
            2. Select voicebank: {request.Options.VoicebankName}
            3. Audio Mixdown (Ctrl+E)
               {request.OutputWavePath}

            This file contains paths and settings only. It does not contain the dialogue text.
            """;
        await File.WriteAllTextAsync(guidePath, guide, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);

        _ = Process.Start(new ProcessStartInfo(guidePath)
        {
            UseShellExecute = true,
        });

        if (!Process.GetProcessesByName("VOCALOID6").Any())
        {
            _ = Process.Start(new ProcessStartInfo(request.Installation.EditorPath)
            {
                UseShellExecute = true,
            });
        }

        await fileWaiter.WaitForWaveAsync(
            request.OutputWavePath,
            TimeSpan.FromSeconds(request.Options.TimeoutSeconds),
            cancellationToken).ConfigureAwait(false);

        return new VocaloidRenderResult(
            request.OutputWavePath,
            nameof(AssistedVocaloidDriver),
            false,
            ["midi-ready", "assisted-guide-ready", "wave-validated"]);
    }
}
