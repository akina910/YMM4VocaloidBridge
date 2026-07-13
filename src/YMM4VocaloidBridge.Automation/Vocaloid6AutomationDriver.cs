using System.Diagnostics;
using System.Windows.Automation;

namespace YMM4VocaloidBridge.Automation;

public sealed class Vocaloid6AutomationDriver(FileReadyWaiter fileWaiter) : IVocaloidDriver
{
    private static readonly SemaphoreSlim AutomationSemaphore = new(1, 1);

    private static class Id
    {
        public const string HomeWindow = "xHomeWindow";
        public const string MainWindow = "xMainWindow";
        public const string AddTrackDialog = "xAddTrackDlg";
        public const string AiTrackButton = "xAiTrackButton";
        public const string VoiceBankComboBox = "xVoiceBankComboBox";
    }

    private static class NativeMethods
    {
        public const uint LeftDown = 0x0002;
        public const uint LeftUp = 0x0004;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point point);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr windowHandle);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    }

    public async Task<VocaloidRenderResult> RenderAsync(
        VocaloidRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await AutomationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        var events = new List<string>();
        AutomationElement? soloButton = null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputWavePath))!);
            if (File.Exists(request.OutputWavePath))
            {
                File.Delete(request.OutputWavePath);
            }

            await Task.Run(
                () => RunUiWorkflow(request, events, cancellationToken, value => soloButton = value),
                cancellationToken).ConfigureAwait(false);
            await fileWaiter.WaitForWaveAsync(
                request.OutputWavePath,
                TimeSpan.FromSeconds(request.Options.TimeoutSeconds),
                cancellationToken).ConfigureAwait(false);
            events.Add("wave-validated");
            return new VocaloidRenderResult(
                request.OutputWavePath,
                nameof(Vocaloid6AutomationDriver),
                false,
                events);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (VocaloidAutomationException exception)
        {
            throw CreateStageException(exception, events);
        }
        catch (Exception exception)
        {
            throw CreateStageException(exception, events);
        }
        finally
        {
            if (soloButton is not null)
            {
                events.Add("stage:restore-solo");
                try
                {
                    await Task.Run(() => RestoreSoloWhenAvailable(soloButton, events), CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    events.Add("solo-restore-failed:" + exception.GetType().Name);
                }
            }

            AutomationSemaphore.Release();
        }
    }

    private static VocaloidAutomationException CreateStageException(
        Exception exception,
        IReadOnlyCollection<string> events)
    {
        var stage = events.LastOrDefault(x => x.StartsWith("stage:", StringComparison.Ordinal))?["stage:".Length..]
            ?? "unknown";
        return new VocaloidAutomationException(
            $"VOCALOID6 UI automation failed at '{stage}' "
                + $"({exception.GetType().Name}: {exception.Message}). "
                + "Assisted mode can continue with the generated MIDI.",
            exception);
    }

    private static void RunUiWorkflow(
        VocaloidRenderRequest request,
        ICollection<string> events,
        CancellationToken cancellationToken,
        Action<AutomationElement> soloButtonReady)
    {
        events.Add("stage:attach-editor");
        var process = AttachOrLaunch(request.Installation.EditorPath);
        events.Add("stage:dismiss-stale-dialogs");
        DismissStaleFileDialogs(process.Id);
        ConfirmVoicebankStyleChange(process.Id, TimeSpan.FromSeconds(5), cancellationToken);
        events.Add("stage:wait-for-editor");
        var mainWindow = WaitUntil(
            () =>
            {
                if (DismissUpdatePrompt(process.Id))
                {
                    return null;
                }

                return FindProcessWindow(process.Id, Id.HomeWindow, Id.MainWindow);
            },
            TimeSpan.FromSeconds(30),
            cancellationToken,
            "VOCALOID6 main window");
        events.Add("editor-ready");

        events.Add("stage:ensure-project");
        var projectVoicebankSelected = EnsureProjectAndVoicebank(
            mainWindow,
            request.Options.VoicebankName,
            cancellationToken);
        events.Add("stage:refresh-editor-window");
        mainWindow = WaitUntil(
            () => FindProcessWindow(process.Id, Id.MainWindow),
            TimeSpan.FromSeconds(15),
            cancellationToken,
            "VOCALOID6 editor window after project setup");
        events.Add("stage:import-midi");
        var importVoicebankSelected = ImportMidi(
            mainWindow,
            request.Artifacts.MidiPath,
            request.Options.VoicebankName,
            cancellationToken);
        events.Add("midi-imported");
        events.Add("stage:select-imported-track-voicebank");
        using var styleWatcherCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var styleWatcher = Task.Run(
            () => ConfirmVoicebankStyleChange(
                process.Id,
                TimeSpan.FromSeconds(30),
                styleWatcherCancellation.Token),
            styleWatcherCancellation.Token);
        bool trackVoicebankSelected;
        try
        {
            trackVoicebankSelected = SelectProcessComboBoxItem(
                process.Id,
                Id.VoiceBankComboBox,
                request.Options.VoicebankName,
                cancellationToken);
            events.Add("stage:confirm-imported-track-style");
            _ = styleWatcher.Wait(TimeSpan.FromSeconds(5));
        }
        finally
        {
            styleWatcherCancellation.Cancel();
        }
        events.Add("stage:verify-imported-track-voicebank");
        var assignedVoicebankConfirmed = IsVoicebankAssigned(mainWindow, request.Options.VoicebankName);
        if (!projectVoicebankSelected
            && !importVoicebankSelected
            && !trackVoicebankSelected
            && !assignedVoicebankConfirmed)
        {
            throw new VocaloidAutomationException(
                $"VOCALOID6 did not expose a verifiable '{request.Options.VoicebankName}' selection during project setup or MIDI import.");
        }

        events.Add("voicebank-selected");
        events.Add("voicebank-selected-and-verified");
        events.Add("stage:enable-solo");
        var soloButton = EnableSoloForLastTrack(mainWindow);
        soloButtonReady(soloButton);
        events.Add("stage:export-wave");
        ExportWave(mainWindow, request.OutputWavePath, events, cancellationToken);
        events.Add("mixdown-requested");
    }

    private static void RestoreSoloWhenAvailable(AutomationElement soloButton, ICollection<string> events)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                if (soloButton.Current.IsEnabled)
                {
                    SetToggle(soloButton, false);
                    events.Add("solo-restored");
                    return;
                }
            }
            catch (ElementNotEnabledException)
            {
            }
            catch (ElementNotAvailableException)
            {
                events.Add("solo-restore-element-unavailable");
                return;
            }

            Thread.Sleep(250);
        }

        events.Add("solo-restore-timed-out");
    }

    private static void DismissStaleFileDialogs(int processId)
    {
        foreach (var dialog in FindAllForProcess(processId, ControlType.Window)
            .Where(x => x.Current.ClassName == "#32770"))
        {
            try
            {
                var cancel = dialog.FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "2")));
                if (cancel is not null)
                {
                    Invoke(cancel);
                }
            }
            catch (ElementNotAvailableException)
            {
            }
            catch (ElementNotEnabledException)
            {
            }
        }
    }

    private static bool DismissUpdatePrompt(int processId)
    {
        var declineButton = FindShallowProcessButton(processId, "いいえ", "No");
        if (declineButton is null)
        {
            return false;
        }

        try
        {
            if (declineButton.Current.IsEnabled)
            {
                Invoke(declineButton);
            }
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (ElementNotEnabledException)
        {
        }

        return true;
    }

    private static Process AttachOrLaunch(string editorPath)
    {
        var process = FindRunningEditor(editorPath, requireMainWindow: false);
        if (process is not null)
        {
            return process;
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

        _ = Process.Start(startInfo)
            ?? throw new VocaloidAutomationException("VOCALOID6 Editor could not be started.");

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(30))
        {
            process = FindRunningEditor(editorPath, requireMainWindow: true);
            if (process is not null)
            {
                return process;
            }

            Thread.Sleep(200);
        }

        throw new VocaloidAutomationException("VOCALOID6 Editor started, but its main process was not found.");
    }

    private static Process? FindRunningEditor(string editorPath, bool requireMainWindow)
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

    private static AutomationElement? FindProcessWindow(int processId, params string[] automationIds)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        var roots = AutomationElement.RootElement
            .FindAll(TreeScope.Children, processCondition)
            .Cast<AutomationElement>()
            .ToArray();
        var identified = roots.FirstOrDefault(x => automationIds.Contains(x.Current.AutomationId, StringComparer.Ordinal));
        if (identified is not null)
        {
            return identified;
        }

        var idCondition = new OrCondition(automationIds
            .Select(id => new PropertyCondition(AutomationElement.AutomationIdProperty, id))
            .Cast<Condition>()
            .ToArray());
        identified = roots
            .Select(root => root.FindFirst(TreeScope.Descendants, idCondition))
            .FirstOrDefault(element => element is not null);
        if (identified is not null)
        {
            return identified;
        }

        if (automationIds.Contains(Id.MainWindow, StringComparer.Ordinal))
        {
            var editor = roots.FirstOrDefault(IsMainWindow);
            if (editor is not null)
            {
                return editor;
            }
        }

        return automationIds.Contains(Id.HomeWindow, StringComparer.Ordinal)
            ? roots.FirstOrDefault(IsHomeWindow)
            : null;
    }

    private static bool ConfirmVoicebankStyleChange(
        int processId,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            AutomationElement? button = null;
            try
            {
                button = FindShallowProcessButton(
                    processId,
                    "スタイルを変更する",
                    "Change Style");
            }
            catch (ElementNotAvailableException)
            {
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }
            if (button is not null)
            {
                try
                {
                    ClickElement(button);
                    return true;
                }
                catch (ElementNotAvailableException)
                {
                }
                catch (ElementNotEnabledException)
                {
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }
            }

            if (timeout == TimeSpan.Zero)
            {
                return false;
            }

            Thread.Sleep(200);
        }
        while (stopwatch.Elapsed < timeout);

        return false;
    }

    private static AutomationElement? FindShallowProcessButton(int processId, params string[] names)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        foreach (AutomationElement root in AutomationElement.RootElement.FindAll(TreeScope.Children, processCondition))
        {
            var match = FindShallowButton(root, names, depth: 0, maximumDepth: 4);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static AutomationElement? FindShallowButton(
        AutomationElement element,
        IReadOnlyCollection<string> names,
        int depth,
        int maximumDepth)
    {
        if (depth > maximumDepth)
        {
            return null;
        }

        try
        {
            var normalizedName = element.Current.Name.TrimStart('_');
            if (element.Current.ControlType == ControlType.Button
                && names.Any(name => string.Equals(normalizedName, name, StringComparison.OrdinalIgnoreCase)))
            {
                return element;
            }

            var child = TreeWalker.ControlViewWalker.GetFirstChild(element);
            while (child is not null)
            {
                var match = FindShallowButton(child, names, depth + 1, maximumDepth);
                if (match is not null)
                {
                    return match;
                }

                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
        }
        catch (ElementNotAvailableException)
        {
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }

        return null;
    }

    private static bool IsMainWindow(AutomationElement window)
    {
        try
        {
            return string.Equals(window.Current.AutomationId, Id.MainWindow, StringComparison.Ordinal)
                || FindDescendantByAutomationId(window, "xMainMenu") is not null
                || FindDescendantByAutomationId(window, "xTrackEditorDiv") is not null
                || FindDescendantByAutomationId(window, "xTrackName") is not null;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    private static bool IsHomeWindow(AutomationElement window)
    {
        try
        {
            return string.Equals(window.Current.AutomationId, Id.HomeWindow, StringComparison.Ordinal)
                || FindByNames(window, ControlType.Button, "NEW PROJECT", "新規プロジェクト", "New Project") is not null
                || FindButtonByDescendantText(window, "NEW PROJECT", "新規プロジェクト", "New Project") is not null;
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    private static bool EnsureProjectAndVoicebank(
        AutomationElement mainWindow,
        string voicebankName,
        CancellationToken cancellationToken)
    {
        AutomationElement? addTrack = null;
        if (IsMainWindow(mainWindow))
        {
            EnsureDedicatedBridgeProject(mainWindow);
            addTrack = FindProcessElementByAutomationId(mainWindow.Current.ProcessId, Id.AddTrackDialog);
            if (addTrack is null)
            {
                return false;
            }
        }

        if (IsHomeWindow(mainWindow))
        {
            var newProject = FindByNames(mainWindow, ControlType.Button, "NEW PROJECT", "新規プロジェクト", "New Project")
                ?? FindButtonByDescendantText(mainWindow, "NEW PROJECT", "新規プロジェクト", "New Project")
                ?? throw new VocaloidAutomationException("The NEW PROJECT button was not found.");
            Invoke(newProject);
        }

        addTrack ??= WaitUntil(
                () => FindProcessElementByAutomationId(mainWindow.Current.ProcessId, Id.AddTrackDialog),
                TimeSpan.FromSeconds(15),
                cancellationToken,
                "Add Track dialog");
        var aiTrack = FindDescendantByAutomationId(addTrack, Id.AiTrackButton)
            ?? throw new VocaloidAutomationException("The VOCALOID:AI track button was not found.");
        Invoke(aiTrack);

        var voicebank = FindDescendantByAutomationId(addTrack, Id.VoiceBankComboBox)
            ?? throw new VocaloidAutomationException("The voicebank selector was not found.");
        SelectComboBoxItem(voicebank, voicebankName);
        InvokeButton(addTrack, "作成", "Create", "OK");
        return true;
    }

    private static bool ImportMidi(
        AutomationElement mainWindow,
        string midiPath,
        string voicebankName,
        CancellationToken cancellationToken)
    {
        var processId = mainWindow.Current.ProcessId;
        var fileMenu = FindByNames(mainWindow, ControlType.MenuItem, "ファイル", "File")
            ?? throw new VocaloidAutomationException("The File menu was not found.");
        Invoke(fileMenu);

        var importMenu = WaitUntil(
            () => FindByNamesForProcess(processId, ControlType.MenuItem, "インポート", "Import"),
            TimeSpan.FromSeconds(5),
            cancellationToken,
            "Import submenu");
        Invoke(importMenu);

        var midiItem = WaitUntil(
            () => FindAllForProcess(processId, ControlType.MenuItem)
                .FirstOrDefault(x => x.Current.Name.Contains("MIDI", StringComparison.OrdinalIgnoreCase)
                    && !x.Current.Name.Contains("エクスポート", StringComparison.OrdinalIgnoreCase)
                    && !x.Current.Name.Contains("Export", StringComparison.OrdinalIgnoreCase)
                    && !x.Current.IsOffscreen),
            TimeSpan.FromSeconds(5),
            cancellationToken,
            "MIDI import menu item");
        Invoke(midiItem);
        CompleteFileDialog(processId, midiPath, isSaveDialog: false, cancellationToken);

        var importSettings = FindTransientWindow(mainWindow, cancellationToken, TimeSpan.FromSeconds(5));
        if (importSettings is not null)
        {
            InvokeButton(importSettings, "OK", "インポート", "Import");
        }

        var mappingDialog = FindTransientWindow(mainWindow, cancellationToken, TimeSpan.FromSeconds(5));
        if (mappingDialog is not null)
        {
            var comboBoxes = FindAllDescendants(mappingDialog, ControlType.ComboBox).ToArray();
            var selected = false;
            foreach (var comboBox in comboBoxes)
            {
                selected |= SelectComboBoxItem(comboBox, voicebankName, required: false);
            }

            InvokeButton(mappingDialog, "OK", "適用", "Apply", "インポート", "Import");
            return selected;
        }

        return false;
    }

    private static void ExportWave(
        AutomationElement mainWindow,
        string outputPath,
        ICollection<string> events,
        CancellationToken cancellationToken)
    {
        var processId = mainWindow.Current.ProcessId;
        events.Add("stage:export-open-file-menu");
        var fileMenu = FindByNames(mainWindow, ControlType.MenuItem, "ファイル", "File")
            ?? throw new VocaloidAutomationException("The File menu was not found before mixdown.");
        Invoke(fileMenu);
        events.Add("stage:export-find-mixdown");
        var mixdown = WaitUntil(
            () => FindAllForProcess(processId, ControlType.MenuItem)
                .FirstOrDefault(x => (x.Current.Name.Contains("Mixdown", StringComparison.OrdinalIgnoreCase)
                        || x.Current.Name.Contains("ミックスダウン", StringComparison.OrdinalIgnoreCase))
                    && x.Current.IsEnabled
                    && !x.Current.IsOffscreen),
            TimeSpan.FromSeconds(60),
            cancellationToken,
            "enabled Audio Mixdown menu item");
        events.Add("stage:export-invoke-mixdown");
        Invoke(mixdown);

        events.Add("stage:export-mixdown-settings");
        var mixdownSettings = FindTransientWindow(mainWindow, cancellationToken, TimeSpan.FromSeconds(5));
        if (mixdownSettings is not null)
        {
            ConfigureMixdownSettings(mixdownSettings);
            InvokeButton(mixdownSettings, "OK");
        }

        events.Add("stage:export-save-dialog");
        CompleteFileDialog(processId, outputPath, isSaveDialog: true, cancellationToken);

        events.Add("stage:export-overwrite-confirmation");
        var overwrite = FindTransientWindow(mainWindow, cancellationToken, TimeSpan.FromSeconds(2));
        if (overwrite is not null && overwrite.Current.Name.Contains("確認", StringComparison.OrdinalIgnoreCase))
        {
            InvokeButton(overwrite, "はい", "Yes", "OK");
        }
    }

    private static void CompleteFileDialog(
        int processId,
        string path,
        bool isSaveDialog,
        CancellationToken cancellationToken)
    {
        var dialog = WaitUntil(
            () => FindAllForProcess(processId, ControlType.Window)
                .FirstOrDefault(x => x.Current.ClassName == "#32770"
                    && x.Current.IsEnabled
                    && !x.Current.IsOffscreen),
            TimeSpan.FromSeconds(10),
            cancellationToken,
            isSaveDialog ? "Save dialog" : "Open dialog");
        var fileNameCondition = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.AutomationIdProperty, isSaveDialog ? "1001" : "1148"));
        var fileName = dialog.FindFirst(TreeScope.Descendants, fileNameCondition)
            ?? throw new VocaloidAutomationException("The filename field was not found.");
        if (!fileName.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
        {
            throw new VocaloidAutomationException("The filename field does not support text input.");
        }

        ((ValuePattern)valuePattern).SetValue(path);
        InvokeButton(dialog, isSaveDialog ? ["保存", "Save"] : ["開く", "Open"]);
    }

    private static AutomationElement? FindTransientWindow(
        AutomationElement mainWindow,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        try
        {
            var mainHandle = mainWindow.Current.NativeWindowHandle;
            return WaitUntil(
                () => FindAllForProcess(mainWindow.Current.ProcessId, ControlType.Window)
                    .FirstOrDefault(x => x.Current.ControlType == ControlType.Window
                        && x.Current.NativeWindowHandle != mainHandle
                        && x.Current.ClassName != "#32770"
                        && x.Current.IsEnabled
                        && !x.Current.IsOffscreen),
                timeout,
                cancellationToken,
                "VOCALOID6 transient dialog");
        }
        catch (VocaloidAutomationException)
        {
            return null;
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return null;
        }
    }

    private static void EnsureDedicatedBridgeProject(AutomationElement mainWindow)
    {
        var trackNameCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "xTrackName");
        var trackNames = mainWindow.FindAll(TreeScope.Descendants, trackNameCondition)
            .Cast<AutomationElement>()
            .SelectMany(x => FindAllDescendants(x, ControlType.Text))
            .Select(x => x.Current.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (trackNames.Any(x => !x.StartsWith("YMM4 Vocaloid Bridge", StringComparison.Ordinal)
            && !string.Equals(x, "VOCALOID:AI", StringComparison.Ordinal)))
        {
            throw new VocaloidAutomationException(
                "The open VOCALOID6 project contains a non-bridge track. Close or save it, then use a dedicated bridge project.");
        }
    }

    private static void ConfigureMixdownSettings(AutomationElement dialog)
    {
        var openExplorer = FindDescendantByAutomationId(dialog, "xOpenExplorerCheckBox");
        if (openExplorer is not null)
        {
            SetToggle(openExplorer, false);
        }
    }

    private static AutomationElement EnableSoloForLastTrack(AutomationElement mainWindow)
    {
        var headerViewer = FindDescendantByAutomationId(mainWindow, "xHeaderViewer");
        if (headerViewer?.TryGetCurrentPattern(ScrollPattern.Pattern, out var scrollPattern) == true)
        {
            var scroll = (ScrollPattern)scrollPattern;
            if (scroll.Current.VerticallyScrollable)
            {
                scroll.SetScrollPercent(ScrollPattern.NoScroll, 100);
                Thread.Sleep(250);
            }
        }

        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, "xSoloButton");
        var soloButton = mainWindow.FindAll(TreeScope.Descendants, condition)
            .Cast<AutomationElement>()
            .LastOrDefault(x => x.Current.IsEnabled)
            ?? throw new VocaloidAutomationException("The imported track Solo button was not found.");
        SetToggle(soloButton, true);
        return soloButton;
    }

    private static void SetToggle(AutomationElement element, bool desiredValue)
    {
        if (!element.TryGetCurrentPattern(TogglePattern.Pattern, out var pattern))
        {
            throw new VocaloidAutomationException($"UI element '{element.Current.AutomationId}' does not support toggling.");
        }

        var toggle = (TogglePattern)pattern;
        var isOn = toggle.Current.ToggleState == ToggleState.On;
        if (isOn != desiredValue)
        {
            toggle.Toggle();
        }
    }

    private static AutomationElement? FindDescendantByAutomationId(AutomationElement root, string automationId)
    {
        return root.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.AutomationIdProperty, automationId));
    }

    private static AutomationElement? FindProcessElementByAutomationId(int processId, string automationId)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        var idCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, automationId);
        return AutomationElement.RootElement
            .FindAll(TreeScope.Children, processCondition)
            .Cast<AutomationElement>()
            .Select(root => string.Equals(root.Current.AutomationId, automationId, StringComparison.Ordinal)
                ? root
                : root.FindFirst(TreeScope.Descendants, idCondition))
            .FirstOrDefault(element => element is not null);
    }

    private static AutomationElement? FindByNames(
        AutomationElement root,
        ControlType controlType,
        params string[] names)
    {
        var matches = FindAllDescendants(root, controlType)
            .Where(x => names.Any(name => string.Equals(x.Current.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return matches.FirstOrDefault(x => !x.Current.IsOffscreen) ?? matches.FirstOrDefault();
    }

    private static AutomationElement? FindByNamesForProcess(
        int processId,
        ControlType controlType,
        params string[] names)
    {
        var matches = FindAllForProcess(processId, controlType)
            .Where(x => names.Any(name => string.Equals(x.Current.Name, name, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        return matches.FirstOrDefault(x => !x.Current.IsOffscreen) ?? matches.FirstOrDefault();
    }

    private static AutomationElement? FindButtonByDescendantText(
        AutomationElement root,
        params string[] names)
    {
        var label = FindAllDescendants(root, ControlType.Text)
            .FirstOrDefault(x => names.Any(name =>
                string.Equals(x.Current.Name, name, StringComparison.OrdinalIgnoreCase)));
        var current = label;
        for (var depth = 0; current is not null && depth < 5; depth++)
        {
            current = TreeWalker.ControlViewWalker.GetParent(current);
            if (current?.Current.ControlType == ControlType.Button)
            {
                return current;
            }
        }

        return null;
    }

    private static IEnumerable<AutomationElement> FindAllDescendants(AutomationElement root, ControlType controlType)
    {
        var condition = new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);
        return root.FindAll(TreeScope.Descendants, condition).Cast<AutomationElement>();
    }

    private static IEnumerable<AutomationElement> FindAllForProcess(int processId, ControlType? controlType)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        var roots = AutomationElement.RootElement
            .FindAll(TreeScope.Children, processCondition)
            .Cast<AutomationElement>()
            .ToArray();
        var typeCondition = controlType is null
            ? Condition.TrueCondition
            : new PropertyCondition(AutomationElement.ControlTypeProperty, controlType);

        foreach (var root in roots)
        {
            if (controlType is null || root.Current.ControlType == controlType)
            {
                yield return root;
            }

            foreach (AutomationElement descendant in root.FindAll(TreeScope.Descendants, typeCondition))
            {
                yield return descendant;
            }
        }
    }

    private static void InvokeButton(AutomationElement root, params string[] names)
    {
        var button = FindAllDescendants(root, ControlType.Button)
            .FirstOrDefault(x => names.Any(name =>
                string.Equals(x.Current.Name, name, StringComparison.OrdinalIgnoreCase)
                || x.Current.Name.StartsWith(name + "(", StringComparison.OrdinalIgnoreCase)))
            ?? throw new VocaloidAutomationException($"Expected button was not found: {string.Join(" / ", names)}");
        Invoke(button);
    }

    private static void Invoke(AutomationElement element)
    {
        if (element.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandPattern))
        {
            ((ExpandCollapsePattern)expandPattern).Expand();
            return;
        }

        if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePattern))
        {
            ((InvokePattern)invokePattern).Invoke();
            return;
        }

        if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPattern))
        {
            ((SelectionItemPattern)selectionPattern).Select();
            return;
        }

        throw new VocaloidAutomationException($"UI element '{element.Current.Name}' cannot be invoked.");
    }

    private static bool SelectComboBoxItem(
        AutomationElement comboBox,
        string itemName,
        bool required = true,
        bool usePointerClick = false)
    {
        if (comboBox.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandPattern))
        {
            ((ExpandCollapsePattern)expandPattern).Expand();
        }

        var item = FindAllDescendants(comboBox, ControlType.ListItem)
            .FirstOrDefault(x => MatchesListItemName(x, itemName))
            ?? FindAllForProcess(comboBox.Current.ProcessId, ControlType.ListItem)
                .FirstOrDefault(x => MatchesListItemName(x, itemName));
        if (item is null)
        {
            if (required)
            {
                throw new VocaloidAutomationException($"Voicebank '{itemName}' was not listed by VOCALOID6.");
            }

            return false;
        }

        if (usePointerClick)
        {
            ClickElement(item);
        }
        else
        {
            Invoke(item);
        }

        return true;
    }

    private static void ClickElement(AutomationElement element)
    {
        var rectangle = element.Current.BoundingRectangle;
        if (rectangle.IsEmpty
            || double.IsInfinity(rectangle.X)
            || double.IsInfinity(rectangle.Y))
        {
            throw new VocaloidAutomationException($"UI element '{element.Current.Name}' does not have a clickable position.");
        }

        var process = Process.GetProcessById(element.Current.ProcessId);
        _ = NativeMethods.SetForegroundWindow(process.MainWindowHandle);
        Thread.Sleep(100);
        _ = NativeMethods.GetCursorPos(out var originalPosition);
        try
        {
            _ = NativeMethods.SetCursorPos(
                (int)Math.Round(rectangle.Left + (rectangle.Width / 2)),
                (int)Math.Round(rectangle.Top + (rectangle.Height / 2)));
            NativeMethods.mouse_event(NativeMethods.LeftDown, 0, 0, 0, UIntPtr.Zero);
            NativeMethods.mouse_event(NativeMethods.LeftUp, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(150);
        }
        finally
        {
            _ = NativeMethods.SetCursorPos(originalPosition.X, originalPosition.Y);
        }
    }

    private static bool SelectProcessComboBoxItem(
        int processId,
        string automationId,
        string itemName,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var comboBox = FindProcessElementByAutomationId(processId, automationId);
                if (comboBox is not null
                    && SelectComboBoxItem(comboBox, itemName, required: false, usePointerClick: true))
                {
                    return true;
                }
            }
            catch (ElementNotAvailableException)
            {
            }
            catch (ElementNotEnabledException)
            {
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            if (ConfirmVoicebankStyleChange(processId, TimeSpan.Zero, cancellationToken))
            {
                return true;
            }

            Thread.Sleep(250);
        }

        throw new VocaloidAutomationException($"Voicebank '{itemName}' could not be selected for the imported track.");
    }

    private static bool MatchesListItemName(AutomationElement item, string itemName)
    {
        try
        {
            return item.Current.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase)
                || FindAllDescendants(item, ControlType.Text)
                    .Any(x => x.Current.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
    }

    private static bool IsVoicebankAssigned(AutomationElement root, string voicebankName)
    {
        try
        {
            if (FindAllDescendants(root, ControlType.Text)
                .Any(element => element.Current.Name.Contains(voicebankName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            foreach (var comboBox in FindAllDescendants(root, ControlType.ComboBox))
            {
                if (comboBox.Current.Name.Contains(voicebankName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (comboBox.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern)
                    && ((ValuePattern)valuePattern).Current.Value.Contains(
                        voicebankName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (comboBox.TryGetCurrentPattern(SelectionPattern.Pattern, out var selectionPattern)
                    && ((SelectionPattern)selectionPattern).Current.GetSelection()
                        .Any(item => MatchesListItemName(item, voicebankName)))
                {
                    return true;
                }
            }

            return FindAllDescendants(root, ControlType.ListItem)
                .Any(item => item.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionItemPattern)
                    && ((SelectionItemPattern)selectionItemPattern).Current.IsSelected
                    && MatchesListItemName(item, voicebankName));
        }
        catch (ElementNotAvailableException)
        {
            return false;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }

    private static T WaitUntil<T>(
        Func<T?> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string description)
        where T : class
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var value = operation();
            if (value is not null)
            {
                return value;
            }

            Thread.Sleep(200);
        }

        throw new VocaloidAutomationException($"Timed out waiting for {description}.");
    }
}
