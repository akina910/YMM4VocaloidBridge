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

                var decline = EnumerateControlView(dialog, maximumDepth: 4)
                    .FirstOrDefault(IsDeclineButton);
                if (decline is null)
                {
                    return false;
                }

                var dialogHandle = IntPtr.Zero;
                try
                {
                    if (!decline.Current.IsEnabled)
                    {
                        return false;
                    }

                    dialogHandle = new IntPtr(dialog.Current.NativeWindowHandle);
                    if (dialogHandle == IntPtr.Zero)
                    {
                        return false;
                    }

                    if (decline.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
                    {
                        ((InvokePattern)pattern).Invoke();
                    }
                    else
                    {
                        var buttonHandle = new IntPtr(decline.Current.NativeWindowHandle);
                        if (buttonHandle == IntPtr.Zero)
                        {
                            return false;
                        }

                        if (NativeMethods.SendMessageTimeout(
                            buttonHandle,
                            NativeMethods.ButtonClick,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            NativeMethods.SendMessageTimeoutFlags.AbortIfHung,
                            2_000,
                            out _) == IntPtr.Zero)
                        {
                            return false;
                        }
                    }

                    return WaitForWindowToClose(dialogHandle, TimeSpan.FromSeconds(2));
                }
                catch (ElementNotAvailableException)
                {
                    return dialogHandle != IntPtr.Zero && !NativeMethods.IsWindow(dialogHandle);
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

    private static bool WaitForWindowToClose(IntPtr windowHandle, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (!NativeMethods.IsWindow(windowHandle))
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return !NativeMethods.IsWindow(windowHandle);
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
            return string.Join(
                '\n',
                EnumerateControlView(dialog, maximumDepth: 4)
                    .Select(element => element.Current.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name)));
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

    private static IEnumerable<AutomationElement> EnumerateControlView(
        AutomationElement root,
        int maximumDepth)
    {
        var pending = new Stack<(AutomationElement Element, int Depth)>();
        pending.Push((root, 0));
        while (pending.Count > 0)
        {
            var (element, depth) = pending.Pop();
            yield return element;
            if (depth >= maximumDepth)
            {
                continue;
            }

            AutomationElement? child;
            try
            {
                child = TreeWalker.ControlViewWalker.GetFirstChild(element);
            }
            catch (ElementNotAvailableException)
            {
                continue;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                continue;
            }

            var children = new List<AutomationElement>();
            while (child is not null)
            {
                children.Add(child);
                try
                {
                    child = TreeWalker.ControlViewWalker.GetNextSibling(child);
                }
                catch (ElementNotAvailableException)
                {
                    break;
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                    break;
                }
            }

            for (var index = children.Count - 1; index >= 0; index--)
            {
                pending.Push((children[index], depth + 1));
            }
        }
    }

    private static bool IsDeclineButton(AutomationElement element)
    {
        try
        {
            return string.Equals(element.Current.AutomationId, "7", StringComparison.Ordinal)
                && string.Equals(element.Current.ClassName, "Button", StringComparison.Ordinal)
                && element.Current.Name is "いいえ(N)" or "いいえ(&N)" or "No(N)" or "No(&N)" or "No";
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

    private static class NativeMethods
    {
        public const uint ButtonClick = 0x00F5;

        [Flags]
        public enum SendMessageTimeoutFlags : uint
        {
            AbortIfHung = 0x0002,
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr windowHandle,
            uint message,
            IntPtr wordParameter,
            IntPtr longParameter,
            SendMessageTimeoutFlags flags,
            uint timeoutMilliseconds,
            out UIntPtr result);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr windowHandle);
    }
}
