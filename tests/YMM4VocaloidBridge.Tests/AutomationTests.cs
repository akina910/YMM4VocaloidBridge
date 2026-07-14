using System.Text;

using YMM4VocaloidBridge.Automation;
using YMM4VocaloidBridge.Core;
using YMM4VocaloidBridge.Core.Audio;

namespace YMM4VocaloidBridge.Tests;

public sealed class AutomationTests : IDisposable
{
    private readonly string temporaryDirectory = Path.Combine(Path.GetTempPath(), "YMM4VocaloidBridgeAutomationTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Miku_selection_detector_matches_only_the_bridge_voice()
    {
        const string miku = """
            {"CurrentCharacter":{"Name":"Miku","Voice":{"API":"YMM4VocaloidBridge.Vocaloid6","Arg":"HATSUNE_MIKU_V6"}}}
            """;
        const string reimu = """
            {"CurrentCharacter":{"Name":"Reimu","Voice":{"API":"AquesTalk","Arg":"f1"}}}
            """;

        Assert.True(MikuSelectionDetector.IsSelected(miku));
        Assert.False(MikuSelectionDetector.IsSelected(reimu));
        Assert.False(MikuSelectionDetector.IsSelected("{}"));
    }

    [Fact]
    public async Task File_waiter_accepts_a_wave_after_it_becomes_stable()
    {
        var path = Path.Combine(temporaryDirectory, "ready.wav");
        var write = Task.Run(async () =>
        {
            await Task.Delay(150);
            WritePcmWave(path);
        });

        await new FileReadyWaiter(new WaveFileValidator())
            .WaitForWaveAsync(path, TimeSpan.FromSeconds(5));
        await write;

        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Automatic_failure_returns_the_assisted_result()
    {
        var artifacts = await SynthesisArtifactBuilder.CreateDefault().BuildAsync(
            "ミクです。",
            new BridgeOptions(),
            temporaryDirectory);
        var request = new VocaloidRenderRequest(
            artifacts,
            new BridgeOptions(),
            Path.Combine(temporaryDirectory, "out.wav"),
            new VocaloidInstallation("editor.exe", "6.12.0", true, "6.12.0"));
        var automatic = new FakeDriver((_, _) => throw new VocaloidAutomationException("test-failure"));
        var assisted = new FakeDriver((renderRequest, _) => Task.FromResult(new VocaloidRenderResult(
            renderRequest.OutputWavePath,
            "fake-assisted",
            false,
            ["assisted-ready"])));
        VocaloidAutomationException? observedFailure = null;

        var result = await new FallbackVocaloidDriver(
            automatic,
            assisted,
            exception =>
            {
                observedFailure = exception;
                return Task.CompletedTask;
            }).RenderAsync(request);

        Assert.True(result.UsedFallback);
        Assert.Equal("fake-assisted", result.DriverName);
        Assert.StartsWith("automatic-failed:", result.Events[0], StringComparison.Ordinal);
        Assert.Equal("test-failure", observedFailure?.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(temporaryDirectory))
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void WritePcmWave(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const int sampleRate = 44_100;
        const int sampleCount = 441;
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        var dataBytes = sampleCount * sizeof(short);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataBytes);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * sizeof(short));
        writer.Write((ushort)sizeof(short));
        writer.Write((ushort)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataBytes);
        for (var index = 0; index < sampleCount; index++)
        {
            writer.Write((short)(index % 100));
        }
    }

    private sealed class FakeDriver(
        Func<VocaloidRenderRequest, CancellationToken, Task<VocaloidRenderResult>> render) : IVocaloidDriver
    {
        public Task<VocaloidRenderResult> RenderAsync(
            VocaloidRenderRequest request,
            CancellationToken cancellationToken = default) => render(request, cancellationToken);
    }
}
