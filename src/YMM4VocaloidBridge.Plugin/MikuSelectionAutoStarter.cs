using System.Reflection;
using System.Text.Json;

using YMM4VocaloidBridge.Automation;
using YMM4VocaloidBridge.Core;

namespace YMM4VocaloidBridge.Plugin;

internal sealed class MikuSelectionAutoStarter
{
    private const string DisableEnvironmentVariable = "YMM4_VOCALOID_BRIDGE_DISABLE_AUTO_START";
    private static readonly Lazy<MikuSelectionAutoStarter> Instance = new(() => new MikuSelectionAutoStarter());
    private static readonly BridgeEventLogger Logger = new();
    private static int startInProgress;

    private readonly SemaphoreSlim checkGate = new(1, 1);
    private readonly string? settingsRoot;
    private readonly Timer? timer;
    private bool wasMikuSelected;

    private MikuSelectionAutoStarter()
    {
        if (IsDisabled())
        {
            return;
        }

        settingsRoot = FindSettingsRoot();
        if (settingsRoot is not null)
        {
            timer = new Timer(_ => _ = CheckCurrentCharacterAsync(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
        }
    }

    public static void EnsureMonitoring() => _ = Instance.Value.timer;

    public static void NotifyVoiceSelected()
    {
        if (!IsDisabled())
        {
            StartEditorInBackground();
        }
    }

    private async Task CheckCurrentCharacterAsync()
    {
        if (!await checkGate.WaitAsync(0).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var path = Directory.EnumerateFiles(
                    settingsRoot!,
                    "YukkuriMovieMaker.Settings.CharacterSettings.json",
                    SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (path is null)
            {
                return;
            }

            string json;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                json = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            var isMikuSelected = MikuSelectionDetector.IsSelected(json);
            if (isMikuSelected && !wasMikuSelected)
            {
                StartEditorInBackground();
            }

            wasMikuSelected = isMikuSelected;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (JsonException)
        {
        }
        finally
        {
            checkGate.Release();
        }
    }

    private static void StartEditorInBackground()
    {
        if (Interlocked.CompareExchange(ref startInProgress, 1, 0) != 0)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var report = new VocaloidInstallationDetector().Detect();
                if (!report.IsReady || report.Installation is null)
                {
                    await Logger.WriteAsync("auto-start-skipped", new { reason = "installation-not-ready" })
                        .ConfigureAwait(false);
                    return;
                }

                var started = new VocaloidEditorWarmup().StartIfNeeded(report.Installation);
                await Logger.WriteAsync("auto-start", new { started }).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await Logger.WriteAsync(
                    "auto-start-failed",
                    new { exception = exception.GetType().Name }).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Exchange(ref startInProgress, 0);
            }
        });
    }

    private static string? FindSettingsRoot()
    {
        var pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var userDirectory = pluginDirectory is null
            ? null
            : Directory.GetParent(pluginDirectory)?.Parent?.FullName;
        var candidate = userDirectory is null ? null : Path.Combine(userDirectory, "setting");
        return candidate is not null && Directory.Exists(candidate) ? candidate : null;
    }

    private static bool IsDisabled() =>
        string.Equals(Environment.GetEnvironmentVariable(DisableEnvironmentVariable), "1", StringComparison.Ordinal);
}
