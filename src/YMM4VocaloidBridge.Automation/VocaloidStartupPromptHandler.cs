using System.Diagnostics;
using System.Windows.Automation;

namespace YMM4VocaloidBridge.Automation;

public static class VocaloidStartupPromptHandler
{
    public const string HomeWindowAutomationId = "xHomeWindow";
    public const string MainWindowAutomationId = "xMainWindow";

    public static bool WaitAndDismissUnlicensedVoicePrompt(int processId, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (TryDismissUnlicensedVoicePrompt(processId))
            {
                return true;
            }

            if (IsEditorReady(processId))
            {
                return false;
            }

            Thread.Sleep(200);
        }

        return false;
    }

    public static bool TryDismissUnlicensedVoicePrompt(int processId)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        foreach (AutomationElement root in AutomationElement.RootElement.FindAll(TreeScope.Children, processCondition))
        {
            foreach (var dialog in EnumerateWindows(root))
            {
                if (!IsModalWin32Dialog(dialog)
                    || !IsUnlicensedVoicePromptText(CollectDialogText(dialog)))
                {
                    continue;
                }

                var decline = dialog.FindFirst(
                    TreeScope.Descendants,
                    new AndCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "7")));
                if (decline is null)
                {
                    return false;
                }

                try
                {
                    if (!decline.Current.IsEnabled
                        || !decline.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
                    {
                        return false;
                    }

                    ((InvokePattern)pattern).Invoke();
                    return true;
                }
                catch (ElementNotAvailableException)
                {
                    return true;
                }
                catch (ElementNotEnabledException)
                {
                    return false;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    return false;
                }
            }
        }

        return false;
    }

    public static bool IsUnlicensedVoicePromptText(string text)
    {
        var isJapanesePrompt = text.Contains("下記のVoiceが認証されていません", StringComparison.Ordinal)
            && text.Contains("VOCALOID Authorizer", StringComparison.OrdinalIgnoreCase)
            && text.Contains("今すぐ認証しますか", StringComparison.Ordinal);
        var isEnglishPrompt = text.Contains("VOCALOID Authorizer", StringComparison.OrdinalIgnoreCase)
            && (text.Contains("not authorized", StringComparison.OrdinalIgnoreCase)
                || text.Contains("not authenticated", StringComparison.OrdinalIgnoreCase))
            && (text.Contains("authorize now", StringComparison.OrdinalIgnoreCase)
                || text.Contains("authenticate now", StringComparison.OrdinalIgnoreCase));
        return isJapanesePrompt || isEnglishPrompt;
    }

    private static IEnumerable<AutomationElement> EnumerateWindows(AutomationElement root)
    {
        yield return root;
        AutomationElementCollection descendants;
        try
        {
            descendants = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window));
        }
        catch (ElementNotAvailableException)
        {
            yield break;
        }

        foreach (AutomationElement descendant in descendants)
        {
            yield return descendant;
        }
    }

    private static string CollectDialogText(AutomationElement dialog)
    {
        try
        {
            var values = new List<string> { dialog.Current.Name };
            values.AddRange(dialog
                .FindAll(
                    TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                .Cast<AutomationElement>()
                .Select(element => element.Current.Name));
            return string.Join('\n', values);
        }
        catch (ElementNotAvailableException)
        {
            return string.Empty;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return string.Empty;
        }
    }

    private static bool IsEditorReady(int processId)
    {
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
        foreach (AutomationElement root in AutomationElement.RootElement.FindAll(TreeScope.Children, processCondition))
        {
            try
            {
                if (root.Current.AutomationId is HomeWindowAutomationId or MainWindowAutomationId
                    || root.FindFirst(
                        TreeScope.Descendants,
                        new OrCondition(
                            new PropertyCondition(AutomationElement.AutomationIdProperty, HomeWindowAutomationId),
                            new PropertyCondition(AutomationElement.AutomationIdProperty, MainWindowAutomationId))) is not null)
                {
                    return true;
                }
            }
            catch (ElementNotAvailableException)
            {
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }
        }

        return false;
    }

    private static bool IsModalWin32Dialog(AutomationElement dialog)
    {
        try
        {
            if (!string.Equals(dialog.Current.ClassName, "#32770", StringComparison.Ordinal)
                || !dialog.TryGetCurrentPattern(WindowPattern.Pattern, out var pattern))
            {
                return false;
            }

            return ((WindowPattern)pattern).Current.IsModal;
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
}
