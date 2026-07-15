using System.Diagnostics;

namespace YMM4VocaloidBridge.Automation;

public sealed class VocaloidEditorWarmup
{
    public bool StartIfNeeded(VocaloidInstallation installation)
    {
        var started = VocaloidEditorProcess.StartIfNeeded(installation.EditorPath);
        var process = VocaloidEditorProcess.AttachOrLaunch(installation.EditorPath);
        _ = VocaloidStartupPromptHandler.WaitAndDismissUnlicensedVoicePrompt(
            process.Id,
            TimeSpan.FromSeconds(90));
        return started;
    }
}

internal static class VocaloidEditorProcess
{
    private static readonly object StartLock = new();
    private static DateTime lastStartAttemptUtc = DateTime.MinValue;

    public static Process AttachOrLaunch(string editorPath)
    {
        var process = FindRunning(editorPath, requireMainWindow: false);
        if (process is not null)
        {
            return process;
        }

        _ = StartIfNeeded(editorPath);

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            process = FindRunning(editorPath, requireMainWindow: true);
            if (process is not null)
            {
                return process;
            }

            Thread.Sleep(200);
        }

        throw new VocaloidAutomationException("VOCALOID6 Editor started, but its main process was not found.");
    }

    public static bool StartIfNeeded(string editorPath)
    {
        lock (StartLock)
        {
            if (FindRunning(editorPath, requireMainWindow: false) is not null
                || DateTime.UtcNow - lastStartAttemptUtc < TimeSpan.FromSeconds(30))
            {
                return false;
            }

            _ = Start(editorPath);
            lastStartAttemptUtc = DateTime.UtcNow;
            return true;
        }
    }

    private static Process Start(string editorPath)
    {
        if (!File.Exists(editorPath))
        {
            throw new FileNotFoundException("VOCALOID6 Editor was not found.", editorPath);
        }

        var startInfo = new ProcessStartInfo(editorPath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(editorPath)!,
        };
        var systemDotnetRoot = FindSystemDotnetRoot(requiredMajorVersion: 8);
        if (systemDotnetRoot is not null)
        {
            startInfo.Environment["DOTNET_ROOT"] = systemDotnetRoot;
            startInfo.Environment["DOTNET_ROOT_X64"] = systemDotnetRoot;
            startInfo.Environment.Remove("DOTNET_HOST_PATH");
        }

        return Process.Start(startInfo)
            ?? throw new VocaloidAutomationException("VOCALOID6 Editor could not be started.");
    }

    private static Process? FindRunning(string editorPath, bool requireMainWindow)
    {
        foreach (var candidate in Process.GetProcessesByName("VOCALOID6")
            .OrderByDescending(GetProcessStartTime))
        {
            try
            {
                if (!candidate.HasExited
                    && string.Equals(candidate.MainModule?.FileName, editorPath, StringComparison.OrdinalIgnoreCase)
                    && (!requireMainWindow || candidate.MainWindowHandle != IntPtr.Zero))
                {
                    return candidate;
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
        }

        return null;
    }

    private static DateTime GetProcessStartTime(Process process)
    {
        try
        {
            return process.StartTime;
        }
        catch (InvalidOperationException)
        {
            return DateTime.MinValue;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return DateTime.MinValue;
        }
    }

    private static string? FindSystemDotnetRoot(int requiredMajorVersion)
    {
        var dotnetRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet");
        return HasRuntime(dotnetRoot, "Microsoft.NETCore.App", requiredMajorVersion)
            && HasRuntime(dotnetRoot, "Microsoft.WindowsDesktop.App", requiredMajorVersion)
                ? dotnetRoot
                : null;
    }

    private static bool HasRuntime(string dotnetRoot, string frameworkName, int requiredMajorVersion)
    {
        var sharedDirectory = Path.Combine(dotnetRoot, "shared", frameworkName);
        if (!Directory.Exists(sharedDirectory))
        {
            return false;
        }

        return Directory.EnumerateDirectories(sharedDirectory)
            .Select(Path.GetFileName)
            .Any(name => Version.TryParse(name, out var version) && version.Major == requiredMajorVersion);
    }
}
