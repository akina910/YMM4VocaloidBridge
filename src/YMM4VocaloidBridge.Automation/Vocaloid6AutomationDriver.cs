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

    public async Task<VocaloidRenderResult> RenderAsync(
        VocaloidRenderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await AutomationSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        var events = new List<string>();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputWavePath))!);
            if (File.Exists(request.OutputWavePath))
            {
                File.Delete(request.OutputWavePath);
            }

            await Task.Run(
                () => RunUiWorkflow(request, events, cancellationToken),
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
        catch (VocaloidAutomationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new VocaloidAutomationException(
                $"VOCALOID6 UI automation failed ({exception.GetType().Name}: {exception.Message}). "
                    + "Assisted mode can continue with the generated MIDI.",
                exception);
        }
        finally
        {
            AutomationSemaphore.Release();
        }
    }

    private static void RunUiWorkflow(
        VocaloidRenderRequest request,
        ICollection<string> events,
        CancellationToken cancellationToken)
    {
        var process = AttachOrLaunch(request.Installation.EditorPath);
        DismissStaleFileDialogs(process.Id);
        var mainWindow = WaitUntil(
            () => FindProcessWindow(process.Id, Id.HomeWindow, Id.MainWindow),
            TimeSpan.FromSeconds(30),
            cancellationToken,
            "VOCALOID6 main window");
        events.Add("editor-ready");

        EnsureProjectAndVoicebank(mainWindow, request.Options.VoicebankName, cancellationToken);
        events.Add("voicebank-selected");
        ImportMidi(mainWindow, request.Artifacts.MidiPath, request.Options.VoicebankName, cancellationToken);
        events.Add("midi-imported");
        var soloButton = EnableSoloForLastTrack(mainWindow);
        try
        {
            ExportWave(mainWindow, request.OutputWavePath, cancellationToken);
            events.Add("mixdown-requested");
        }
        finally
        {
            SetToggle(soloButton, false);
        }
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

    private static Process AttachOrLaunch(string editorPath)
    {
        var process = Process.GetProcessesByName("VOCALOID6").FirstOrDefault();
        if (process is not null)
        {
            return process;
        }

        return Process.Start(new ProcessStartInfo(editorPath) { UseShellExecute = true })
            ?? throw new VocaloidAutomationException("VOCALOID6 Editor could not be started.");
    }

    private static AutomationElement? FindProcessWindow(int processId, params string[] automationIds)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        return AutomationElement.RootElement
            .FindAll(TreeScope.Children, processCondition)
            .Cast<AutomationElement>()
            .FirstOrDefault(x => automationIds.Contains(x.Current.AutomationId, StringComparer.Ordinal));
    }

    private static void EnsureProjectAndVoicebank(
        AutomationElement mainWindow,
        string voicebankName,
        CancellationToken cancellationToken)
    {
        if (mainWindow.Current.AutomationId == Id.MainWindow)
        {
            EnsureDedicatedBridgeProject(mainWindow);
            return;
        }

        if (mainWindow.Current.AutomationId == Id.HomeWindow)
        {
            var newProject = FindByNames(mainWindow, ControlType.Button, "NEW PROJECT", "新規プロジェクト", "New Project")
                ?? throw new VocaloidAutomationException("The NEW PROJECT button was not found.");
            Invoke(newProject);
        }

        var addTrack = WaitUntil(
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
    }

    private static void ImportMidi(
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
            foreach (var comboBox in FindAllDescendants(mappingDialog, ControlType.ComboBox))
            {
                SelectComboBoxItem(comboBox, voicebankName, required: false);
            }

            InvokeButton(mappingDialog, "OK", "適用", "Apply", "インポート", "Import");
        }
    }

    private static void ExportWave(
        AutomationElement mainWindow,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var processId = mainWindow.Current.ProcessId;
        var fileMenu = FindByNames(mainWindow, ControlType.MenuItem, "ファイル", "File")
            ?? throw new VocaloidAutomationException("The File menu was not found before mixdown.");
        Invoke(fileMenu);
        var mixdown = WaitUntil(
            () => FindAllForProcess(processId, ControlType.MenuItem)
                .FirstOrDefault(x => x.Current.Name.Contains("Mixdown", StringComparison.OrdinalIgnoreCase)
                    || x.Current.Name.Contains("ミックスダウン", StringComparison.OrdinalIgnoreCase)),
            TimeSpan.FromSeconds(5),
            cancellationToken,
            "Audio Mixdown menu item");
        Invoke(mixdown);

        var mixdownSettings = FindTransientWindow(mainWindow, cancellationToken, TimeSpan.FromSeconds(5));
        if (mixdownSettings is not null)
        {
            ConfigureMixdownSettings(mixdownSettings);
            InvokeButton(mixdownSettings, "OK");
        }

        CompleteFileDialog(processId, outputPath, isSaveDialog: true, cancellationToken);

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
        return FindAllForProcess(processId, controlType: null)
            .FirstOrDefault(x => string.Equals(x.Current.AutomationId, automationId, StringComparison.Ordinal));
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

    private static void SelectComboBoxItem(AutomationElement comboBox, string itemName, bool required = true)
    {
        if (comboBox.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var expandPattern))
        {
            ((ExpandCollapsePattern)expandPattern).Expand();
        }

        var item = FindAllDescendants(comboBox, ControlType.ListItem)
            .FirstOrDefault(x => string.Equals(x.Current.Name, itemName, StringComparison.OrdinalIgnoreCase))
            ?? FindAllForProcess(comboBox.Current.ProcessId, ControlType.ListItem)
                .FirstOrDefault(x => x.Current.Name.Contains(itemName, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            if (required)
            {
                throw new VocaloidAutomationException($"Voicebank '{itemName}' was not listed by VOCALOID6.");
            }

            return;
        }

        Invoke(item);
        if (comboBox.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out expandPattern))
        {
            ((ExpandCollapsePattern)expandPattern).Collapse();
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
