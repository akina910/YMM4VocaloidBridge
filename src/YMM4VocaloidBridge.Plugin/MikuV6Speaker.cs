using System.IO;

using YukkuriMovieMaker.Plugin.Voice;

using YMM4VocaloidBridge.Automation;
using YMM4VocaloidBridge.Core;
using YMM4VocaloidBridge.Core.Audio;
using YMM4VocaloidBridge.Core.Caching;
using YMM4VocaloidBridge.Core.LipSync;
using YMM4VocaloidBridge.Core.Reading;

namespace YMM4VocaloidBridge.Plugin;

public sealed class MikuV6Speaker : IVoiceSpeaker
{
    private static readonly SemaphoreSlim SynthesisSemaphore = new(1, 1);
    private static readonly string ApplicationDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "YMM4VocaloidBridge");

    private readonly JapaneseReadingService readingService = new();
    private readonly VocaloidInstallationDetector installationDetector = new();
    private readonly WaveFileValidator waveValidator = new();
    private readonly WaveAudioAnalyzer waveAudioAnalyzer = new();
    private readonly LipSyncTimelineAligner lipSyncTimelineAligner = new();
    private readonly LabWriter labWriter = new();
    private readonly BridgeEventLogger logger = new();

    public string EngineName => "VOCALOID6 Bridge";

    public string SpeakerName => "初音ミク V6 ORIGINAL";

    public string API => "YMM4VocaloidBridge.Vocaloid6";

    public string ID => "HATSUNE_MIKU_V6";

    public bool IsVoiceDataCachingRequired => true;

    public SupportedTextFormat Format => SupportedTextFormat.Custom;

    public IVoiceLicense? License => null;

    public IVoiceResource? Resource => null;

    public string? SpeakerAuthor => "Crypton Future Media, INC.";

    public string? SpeakerContentId => null;

    public string? EngineAuthor => "YMM4VocaloidBridge contributors";

    public string? EngineContentId => null;

    public bool IsMatch(string api, string id) => api == API && id == ID;

    public IVoiceParameter CreateVoiceParameter()
    {
        MikuSelectionAutoStarter.NotifyVoiceSelected();
        return new MikuV6VoiceParameter();
    }

    public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter) =>
        currentParameter is MikuV6VoiceParameter ? currentParameter : new MikuV6VoiceParameter();

    public Task<string> ConvertKanjiToYomiAsync(string text, IVoiceParameter voiceParameter)
    {
        return Task.FromResult(readingService.Convert(text).Pronunciation);
    }

    public async Task<IVoicePronounce?> CreateVoiceAsync(
        string text,
        IVoicePronounce? pronounce,
        IVoiceParameter? parameter,
        string filePath)
    {
        await SynthesisSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var settings = parameter as MikuV6VoiceParameter ?? new MikuV6VoiceParameter();
            var options = settings.ToOptions().Validate();
            var report = installationDetector.Detect();
            if (!report.IsReady || report.Installation is null)
            {
                throw new InvalidOperationException(
                    "VOCALOID6 Editor and HATSUNE MIKU V6 are required. Run the bridge doctor command for details.");
            }

            var reading = readingService.Convert(text);
            var cacheKey = SynthesisCacheKey.Create(reading.NormalizedText, options, report.Installation.EditorVersion);
            var cache = new SynthesisCache(Path.Combine(ApplicationDataDirectory, "cache"));
            var workDirectory = Path.Combine(ApplicationDataDirectory, "work", cacheKey);
            Directory.CreateDirectory(workDirectory);
            CleanupOldWorkspaces(Path.Combine(ApplicationDataDirectory, "work"), TimeSpan.FromDays(7));
            var waveOutput = SynthesisWaveOutput.Create(filePath, workDirectory);

            var artifacts = await SynthesisArtifactBuilder.CreateDefault()
                .BuildAsync(text, options, workDirectory)
                .ConfigureAwait(false);
            if (await cache.TryRestoreAsync(cacheKey, filePath).ConfigureAwait(false))
            {
                try
                {
                    _ = waveValidator.Validate(filePath);
                    var cachedPronounce = await CreateAlignedPronounceAsync(artifacts, filePath, options).ConfigureAwait(false);
                    await logger.WriteAsync(
                        "cache-hit",
                        new { frames = cachedPronounce.LipSyncFrames.Length }).ConfigureAwait(false);
                    return cachedPronounce;
                }
                catch (InvalidDataException)
                {
                    cache.Remove(cacheKey);
                    File.Delete(filePath);
                    await logger.WriteAsync("cache-rejected", new { reason = "invalid-or-silent-wave" })
                        .ConfigureAwait(false);
                }
            }

            var waiter = new FileReadyWaiter(waveValidator);
            IVocaloidDriver assisted = new AssistedVocaloidDriver(waiter);
            var driver = VocaloidDriverFactory.Create(
                options.DriverMode,
                new Vocaloid6AutomationDriver(waiter),
                assisted,
                allowAutomaticFallback: false);
            await logger.WriteAsync(
                "render-start",
                new
                {
                    options.DriverMode,
                    requestedExtension = Path.GetExtension(waveOutput.RequestedPath),
                    renderExtension = Path.GetExtension(waveOutput.RenderPath),
                }).ConfigureAwait(false);
            var request = new VocaloidRenderRequest(artifacts, options, waveOutput.RenderPath, report.Installation);
            VocaloidRenderResult render;
            try
            {
                render = await driver.RenderAsync(request).ConfigureAwait(false);
            }
            catch (Exception exception) when (
                options.DriverMode == VocaloidDriverMode.Automatic
                && exception is not OperationCanceledException)
            {
                await logger.WriteAsync("automatic-failed", new { exception.Message }).ConfigureAwait(false);
                throw;
            }

            _ = waveValidator.Validate(waveOutput.RenderPath);
            await waveOutput.PublishAsync().ConfigureAwait(false);
            _ = waveValidator.Validate(waveOutput.RequestedPath);
            var resultPronounce = await CreateAlignedPronounceAsync(
                artifacts,
                waveOutput.RequestedPath,
                options).ConfigureAwait(false);
            await cache.StoreAsync(cacheKey, filePath).ConfigureAwait(false);
            await logger.WriteAsync(
                "render-complete",
                new
                {
                    driver = render.DriverName,
                    render.UsedFallback,
                    notes = artifacts.Sequence.Notes.Count,
                    frames = artifacts.LipSyncFrames.Count,
                    editorVersion = report.Installation.EditorVersion,
                }).ConfigureAwait(false);
            return resultPronounce;
        }
        catch (Exception exception)
        {
            await logger.WriteAsync(
                "render-failed",
                new { exception = exception.GetType().Name }).ConfigureAwait(false);
            throw new InvalidOperationException(
                "初音ミク V6 ORIGINALの音声生成に失敗しました。YMM4 Vocaloid Bridgeのdoctorとログを確認してください。",
                exception);
        }
        finally
        {
            SynthesisSemaphore.Release();
        }
    }

    private async Task<MikuV6Pronounce> CreateAlignedPronounceAsync(
        SynthesisArtifacts artifacts,
        string wavePath,
        BridgeOptions options)
    {
        var activity = waveAudioAnalyzer.Analyze(wavePath);
        var frames = lipSyncTimelineAligner.Align(
            artifacts.LipSyncFrames,
            activity,
            TimeSpan.FromMilliseconds(options.LipSyncLeadMilliseconds));
        await labWriter.WriteAsync(frames, artifacts.LabPath).ConfigureAwait(false);
        await logger.WriteAsync(
            "lip-sync-aligned",
            new
            {
                frames = frames.Count,
                activeStartMilliseconds = activity.ActiveStart.TotalMilliseconds,
                activeEndMilliseconds = activity.ActiveEnd.TotalMilliseconds,
                durationMilliseconds = activity.Format.Duration.TotalMilliseconds,
                options.LipSyncLeadMilliseconds,
            }).ConfigureAwait(false);
        return new MikuV6Pronounce(frames);
    }

    private static void CleanupOldWorkspaces(string rootDirectory, TimeSpan maximumAge)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return;
        }

        var threshold = DateTime.UtcNow - maximumAge;
        foreach (var directory in Directory.EnumerateDirectories(rootDirectory))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(directory) < threshold)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
