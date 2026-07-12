using System.Diagnostics;
using System.Windows.Automation;

namespace YMM4VocaloidBridge.Automation;

public sealed record VocaloidUiElement(
    int Depth,
    string ControlType,
    string Name,
    string AutomationId,
    string ClassName);

public sealed class VocaloidUiInspector
{
    public IReadOnlyList<string> CaptureTrackNames()
    {
        var process = Process.GetProcessesByName("VOCALOID6").FirstOrDefault()
            ?? throw new InvalidOperationException("VOCALOID6 Editor is not running.");
        var roots = AutomationElement.RootElement
            .FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id))
            .Cast<AutomationElement>();
        var condition = new PropertyCondition(AutomationElement.AutomationIdProperty, "xTrackName");
        return roots
            .SelectMany(root => root.FindAll(TreeScope.Descendants, condition).Cast<AutomationElement>())
            .Select(element => new
            {
                element.Current.Name,
                Children = element.FindAll(
                        TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                    .Cast<AutomationElement>()
                    .Select(child => child.Current.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToArray(),
            })
            .SelectMany(x => string.IsNullOrWhiteSpace(x.Name) ? x.Children : [x.Name])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<VocaloidUiElement> Capture(
        int maximumDepth = 6,
        int maximumElements = 1000,
        string? openMenuName = null)
    {
        var process = Process.GetProcessesByName("VOCALOID6").FirstOrDefault()
            ?? throw new InvalidOperationException("VOCALOID6 Editor is not running.");
        var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id);
        var roots = AutomationElement.RootElement
            .FindAll(TreeScope.Children, processCondition)
            .Cast<AutomationElement>()
            .ToArray();
        if (!string.IsNullOrWhiteSpace(openMenuName))
        {
            var menuCondition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.NameProperty, openMenuName));
            var menu = roots.Select(x => x.FindFirst(TreeScope.Descendants, menuCondition)).FirstOrDefault(x => x is not null)
                ?? throw new InvalidOperationException($"Menu '{openMenuName}' was not found.");
            if (!menu.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var pattern))
            {
                throw new InvalidOperationException($"Menu '{openMenuName}' cannot be expanded.");
            }

            ((ExpandCollapsePattern)pattern).Expand();
            Thread.Sleep(500);
            roots = AutomationElement.RootElement
                .FindAll(TreeScope.Children, processCondition)
                .Cast<AutomationElement>()
                .ToArray();
        }
        var result = new List<VocaloidUiElement>();
        foreach (var root in roots)
        {
            CaptureChildren(root, 0, maximumDepth, maximumElements, result);
        }

        return result;
    }

    private static void CaptureChildren(
        AutomationElement element,
        int depth,
        int maximumDepth,
        int maximumElements,
        ICollection<VocaloidUiElement> result)
    {
        if (depth > maximumDepth || result.Count >= maximumElements)
        {
            return;
        }

        try
        {
            result.Add(new VocaloidUiElement(
                depth,
                element.Current.ControlType.ProgrammaticName.Replace("ControlType.", string.Empty, StringComparison.Ordinal),
                element.Current.Name,
                element.Current.AutomationId,
                element.Current.ClassName));

            var child = TreeWalker.ControlViewWalker.GetFirstChild(element);
            while (child is not null && result.Count < maximumElements)
            {
                CaptureChildren(child, depth + 1, maximumDepth, maximumElements, result);
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
        }
        catch (ElementNotAvailableException)
        {
        }
    }
}
