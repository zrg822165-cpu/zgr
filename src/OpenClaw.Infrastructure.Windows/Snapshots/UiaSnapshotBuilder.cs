using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;
using FlaUI.UIA3;
using OpenClaw.Core.Abstractions;
using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;
using OpenClaw.Core.Snapshots;
using OpenClaw.Infrastructure.Windows.Windows;

namespace OpenClaw.Infrastructure.Windows.Snapshots;

public sealed class UiaSnapshotBuilder : ISnapshotBuilder, IDisposable
{
    private readonly UIA3Automation _automation = new();
    private readonly IWindowRegistry _windowRegistry;
    private readonly IRefRegistry _refRegistry;
    private readonly ISnapshotFormatter _formatter;

    public UiaSnapshotBuilder(IWindowRegistry windowRegistry, IRefRegistry refRegistry, ISnapshotFormatter formatter)
    {
        _windowRegistry = windowRegistry;
        _refRegistry = refRegistry;
        _formatter = formatter;
    }

    public SnapshotResult BuildActiveWindowSnapshot()
    {
        var activeWindow = _windowRegistry.GetActiveWindow() ?? throw new InvalidOperationException("No active window found.");
        return BuildSnapshot(activeWindow);
    }

    public SnapshotResult BuildWindowSnapshot(WindowRef windowRef)
    {
        var window = _windowRegistry.ListWindows().FirstOrDefault(candidate => candidate.Ref == windowRef)
            ?? throw new InvalidOperationException($"Window not found: {windowRef}");

        return BuildSnapshot(window);
    }

    public void Dispose()
    {
        _automation.Dispose();
    }

    private SnapshotResult BuildSnapshot(WindowSummary windowSummary)
    {
        var snapshotVersion = $"snap-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}";
        var handle = ResolveHandle(windowSummary.Ref);
        var window = _automation.FromHandle(handle)?.AsWindow() ?? throw new InvalidOperationException("Unable to open target window.");
        var focusedElement = _automation.FocusedElement();
        var root = BuildElementNode(window, windowSummary.Ref, snapshotVersion);
        var focusedRef = focusedElement is null ? null : TryFindFocusedRef(root, focusedElement);
        var summaryText = _formatter.Format(windowSummary, root, snapshotVersion, focusedRef);

        return new SnapshotResult(
            windowSummary.Ref,
            snapshotVersion,
            root,
            summaryText,
            new Dictionary<string, string?>
            {
                ["backend_used"] = "uia",
                ["view_used"] = "control",
                ["snapshot_version"] = snapshotVersion,
            });
    }

    private nint ResolveHandle(WindowRef windowRef)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var windows = NativeMethods.EnumerateVisibleTopLevelWindows();
            foreach (var handle in windows)
            {
                if (_refRegistry.GetOrCreateWindowRef(handle) == windowRef)
                {
                    return handle;
                }
            }

            Thread.Sleep(100);
        }

        throw new InvalidOperationException($"Unable to resolve native handle for {windowRef}.");
    }
    private ElementNode BuildElementNode(AutomationElement element, WindowRef windowRef, string snapshotVersion)
    {
        var children = GetChildElements(element)
            .Select(child => BuildElementNode(child, windowRef, snapshotVersion))
            .ToList();

        var actions = InferActions(element);
        var states = InferStates(element);
        var elementRef = _refRegistry.CreateElementRef(windowRef, element, "uia", snapshotVersion);
        var bounds = GetOptionalBounds(element);
        var role = GetOptionalRole(element) ?? "Unknown";

        return new ElementNode(
            elementRef,
            role,
            GetOptionalProperty(() => element.Name),
            GetOptionalProperty(() => element.AutomationId),
            GetOptionalProperty(() => element.ClassName),
            bounds,
            states,
            actions,
            children);
    }

    private static IReadOnlyList<string> InferActions(AutomationElement element)
    {
        var actions = new List<string>();

        if (ProbeSupport(() => SupportsInvoke(element)))
        {
            actions.Add("invoke");
        }

        if (ProbeSupport(() => SupportsValue(element)))
        {
            actions.Add("set_value");
        }

        if (ProbeSupport(() => SupportsRangeValue(element)))
        {
            actions.Add("range_value");
        }

        if (ProbeSupport(() => SupportsSelection(element)))
        {
            actions.Add("select");
        }

        if (ProbeSupport(() => SupportsToggle(element)))
        {
            actions.Add("toggle");
        }

        if (ProbeSupport(() => SupportsExpandCollapse(element)))
        {
            actions.Add("expand");
            actions.Add("collapse");
        }

        if (ProbeSupport(() => element.Properties.IsKeyboardFocusable.ValueOrDefault))
        {
            actions.Add("focus");
        }

        return actions;
    }

    private static bool SupportsInvoke(AutomationElement element)
    {
        try
        {
            if (!element.Patterns.Invoke.IsSupported)
            {
                return false;
            }

            _ = element.Patterns.Invoke.Pattern;
            return true;
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool SupportsValue(AutomationElement element)
    {
        try
        {
            if (!element.Patterns.Value.IsSupported)
            {
                return false;
            }

            _ = element.Patterns.Value.Pattern;
            return true;
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool SupportsToggle(AutomationElement element)
    {
        try
        {
            if (!element.Patterns.Toggle.IsSupported)
            {
                return false;
            }

            _ = element.Patterns.Toggle.Pattern.ToggleState;
            return true;
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool SupportsRangeValue(AutomationElement element)
    {
        try
        {
            if (!element.Patterns.RangeValue.IsSupported)
            {
                return false;
            }

            var pattern = element.Patterns.RangeValue.Pattern;
            _ = pattern.Value;
            return !pattern.IsReadOnly;
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool SupportsExpandCollapse(AutomationElement element)
    {
        try
        {
            // Be conservative on generic buttons: some web/Radix surfaces advertise
            // expand/collapse metadata that does not map to a usable UIA pattern.
            if (IsControlType(element, FlaUI.Core.Definitions.ControlType.Button))
            {
                return false;
            }

            if (!element.Patterns.ExpandCollapse.IsSupported)
            {
                return false;
            }

            _ = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState;
            return true;
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool SupportsSelection(AutomationElement element)
    {
        try
        {
            if (element.Patterns.Selection.IsSupported)
            {
                _ = element.Patterns.Selection.Pattern;
                return true;
            }

            if (element.Patterns.SelectionItem.IsSupported)
            {
                _ = element.Patterns.SelectionItem.Pattern;
                return true;
            }

            return false;
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool IsControlType(AutomationElement element, FlaUI.Core.Definitions.ControlType controlType)
    {
        try
        {
            return element.ControlType == controlType;
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static bool ProbeSupport(Func<bool> probe)
    {
        try
        {
            return probe();
        }
        catch (PropertyNotSupportedException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> InferStates(AutomationElement element)
    {
        var states = new List<string>();

        if (GetOptionalBoolProperty(() => element.Properties.IsEnabled.ValueOrDefault) is true)
        {
            states.Add("enabled");
        }

        var isOffscreen = GetOptionalBoolProperty(() => element.Properties.IsOffscreen.ValueOrDefault);
        if (isOffscreen is false)
        {
            states.Add("visible");
        }

        if (GetOptionalBoolProperty(() => element.Properties.HasKeyboardFocus.ValueOrDefault) is true)
        {
            states.Add("focused");
        }

        return states;
    }

    private static ElementRef? TryFindFocusedRef(ElementNode root, AutomationElement focusedElement)
    {
        if (Matches(root, focusedElement))
        {
            return root.Ref;
        }

        foreach (var child in root.Children)
        {
            var match = TryFindFocusedRef(child, focusedElement);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool Matches(ElementNode node, AutomationElement focusedElement)
    {
        return string.Equals(node.Name, GetOptionalProperty(() => focusedElement.Name), StringComparison.Ordinal)
            && string.Equals(node.AutomationId, GetOptionalProperty(() => focusedElement.AutomationId), StringComparison.Ordinal)
            && string.Equals(node.ClassName, GetOptionalProperty(() => focusedElement.ClassName), StringComparison.Ordinal)
            && string.Equals(node.Role, GetOptionalRole(focusedElement), StringComparison.Ordinal);
    }

    private static IReadOnlyList<AutomationElement> GetChildElements(AutomationElement element)
    {
        try
        {
            return element.FindAllChildren();
        }
        catch (COMException)
        {
            return Array.Empty<AutomationElement>();
        }
    }

    private static string? GetOptionalRole(AutomationElement element)
    {
        try
        {
            return element.ControlType.ToString();
        }
        catch (PropertyNotSupportedException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static ElementBounds? GetOptionalBounds(AutomationElement element)
    {
        try
        {
            var bounds = element.BoundingRectangle;
            return bounds.IsEmpty
                ? null
                : new ElementBounds((int)bounds.Left, (int)bounds.Top, (int)bounds.Width, (int)bounds.Height);
        }
        catch (PropertyNotSupportedException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static string? GetOptionalProperty(Func<string?> accessor)
    {
        try
        {
            return accessor();
        }
        catch (PropertyNotSupportedException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static bool? GetOptionalBoolProperty(Func<bool> accessor)
    {
        try
        {
            return accessor();
        }
        catch (PropertyNotSupportedException)
        {
            return null;
        }
        catch (COMException)
        {
            return null;
        }
    }
}
