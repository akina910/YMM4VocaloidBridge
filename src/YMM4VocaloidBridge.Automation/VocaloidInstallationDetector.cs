using System.Diagnostics;

using Microsoft.Win32;

namespace YMM4VocaloidBridge.Automation;

public sealed record VocaloidInstallation(
    string EditorPath,
    string EditorVersion,
    bool HasHatsuneMikuV6,
    string? MikuVersion);

public sealed record InstallationDiagnostic(string Code, bool Success, string Message);

public sealed record InstallationReport(VocaloidInstallation? Installation, IReadOnlyList<InstallationDiagnostic> Diagnostics)
{
    public bool IsReady => Installation is { HasHatsuneMikuV6: true };
}

public sealed class VocaloidInstallationDetector
{
    public InstallationReport Detect()
    {
        var diagnostics = new List<InstallationDiagnostic>();
        var editorPath = FindEditorPath();
        if (editorPath is null)
        {
            diagnostics.Add(new InstallationDiagnostic("editor", false, "VOCALOID6 Editor was not found."));
            return new InstallationReport(null, diagnostics);
        }

        var editorVersion = FileVersionInfo.GetVersionInfo(editorPath).FileVersion ?? "unknown";
        diagnostics.Add(new InstallationDiagnostic("editor", true, $"VOCALOID6 Editor {editorVersion}"));

        var mikuVersion = FindInstalledProductVersion("HATSUNE MIKU V6")
            ?? FindInstalledProductVersion("初音ミク V6");
        var hasMiku = mikuVersion is not null;
        diagnostics.Add(new InstallationDiagnostic(
            "voicebank",
            hasMiku,
            hasMiku ? $"HATSUNE MIKU V6 {mikuVersion}" : "HATSUNE MIKU V6 was not found in installed products."));

        return new InstallationReport(
            new VocaloidInstallation(editorPath, editorVersion, hasMiku, mikuVersion),
            diagnostics);
    }

    private static string? FindEditorPath()
    {
        var environmentPath = Environment.GetEnvironmentVariable("VOCALOID6_EDITOR");
        var candidates = new[]
        {
            environmentPath,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VOCALOID6", "Editor", "VOCALOID6.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VOCALOID6", "Editor", "VOCALOID6.exe"),
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static string? FindInstalledProductVersion(string productName)
    {
        var views = Environment.Is64BitOperatingSystem
            ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
            : new[] { RegistryView.Registry32 };

        foreach (var view in views)
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall is null)
            {
                continue;
            }

            foreach (var subKeyName in uninstall.GetSubKeyNames())
            {
                using var product = uninstall.OpenSubKey(subKeyName);
                var displayName = product?.GetValue("DisplayName") as string;
                if (displayName?.Contains(productName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return product?.GetValue("DisplayVersion") as string ?? "installed";
                }
            }
        }

        return null;
    }
}
