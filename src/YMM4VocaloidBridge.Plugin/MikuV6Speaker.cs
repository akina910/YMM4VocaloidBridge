using System.IO;

using YukkuriMovieMaker.Plugin.Voice;

using YMM4VocaloidBridge.Automation;
using YMM4VocaloidBridge.Core;
using YMM4VocaloidBridge.Core.Audio;
using YMM4VocaloidBridge.Core.Caching;
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
    private readonly BridgeEventLogger logger = new();

    public string EngineName => "VOCALOID6 Bridge";

    public string SpeakerName => "初音ミク V6";

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

    public IVoiceParameter CreateVoiceParameter() => new MikuV6VoiceParameter();

    public IVoiceParameter MigrateParameter(IVoiceParameter currentParameter) =>
        currentParameter is MikuV6VoiceParameter ? currentParameter : CreateVoiceParameter();

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

            var artifacts = await SynthesisArtifactBuilder.CreateDefault()
                .BuildAsync(text, options, workDirectory)
                .ConfigureAwait(false);
            var resultPronounce = new MikuV6Pronounce(artifacts.LipSyncFrames);

            if (await cache.TryRestoreAsync(cacheKey, filePath).ConfigureAwait(false))
            {
                _ = waveValidator.Validate(filePath);
                await logger.WriteAsync("cache-hit", new { frames = artifacts.LipSyncFrames.Count }).ConfigureAwait(false);
                return resultPronounce;
            }

            var waiter = new FileReadyWaiter(waveValidator);
            IVocaloidDriver assisted = new AssistedVocaloidDriver(waiter);
            IVocaloidDriver driver = options.DriverMode == VocaloidDriverMode.Automatic
                ? new FallbackVocaloidDriver(new Vocaloid6AutomationDriver(waiter), assisted)
                : assisted;
            var request = new VocaloidRenderRequest(artifacts, options, filePath, report.Installation);
            var render = await driver.RenderAsync(request).ConfigureAwait(false);
            _ = waveValidator.Validate(filePath);
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
                "初音ミク V6の音声生成に失敗しました。YMM4 Vocaloid Bridgeのdoctorとログを確認してください。",
                exception);
        }
        finally
        {
            SynthesisSemaphore.Release();
        }
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
