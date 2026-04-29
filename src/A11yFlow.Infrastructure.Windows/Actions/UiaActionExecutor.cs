using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.Exceptions;
using FlaUI.Core.WindowsAPI;
using A11yFlow.Core.Abstractions;
using A11yFlow.Core.Actions;
using A11yFlow.Core.Errors;
using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;
using A11yFlow.Infrastructure.Windows.Windows;
using System.Globalization;
using System.Runtime.InteropServices;

namespace A11yFlow.Infrastructure.Windows.Actions;

public sealed class UiaActionExecutor : IActionExecutor
{
    private readonly IWindowRegistry _windowRegistry;

    public UiaActionExecutor(IWindowRegistry windowRegistry)
    {
        _windowRegistry = windowRegistry;
    }

    public ToolResult<ActionExecutionResult> Execute(ActionRequest request, ResolvedTarget target)
    {
        if (target.Entry.AutomationElement is not AutomationElement element)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.TargetStale,
                "Resolved target no longer has a live UIA automation element.",
                true,
                "Refresh the snapshot and retry the action.",
                new Dictionary<string, string?>()));
        }

        var isOffscreen = element.Properties.IsOffscreen.ValueOrDefault;
        if (request.ExecutionPolicy.RequireVisibility && isOffscreen)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.VisibilityRequired,
                "Target is offscreen and the execution policy requires visibility.",
                true,
                "Bring the target into view or disable require_visibility.",
                new Dictionary<string, string?>()));
        }

        try
        {
            return request.Action.ToLowerInvariant() switch
            {
                "invoke" => ExecuteInvoke(request, target.Entry.WindowRef, element),
                "focus" => ExecuteFocus(request, target.Entry.WindowRef, element),
                "set_value" => ExecuteSetValue(request, element),
                "range_value" => ExecuteRangeValue(request, element),
                "select" => ExecuteSelect(request, element),
                "toggle" => ExecuteToggle(request, element),
                "expand" => ExecuteExpandCollapse(element, expand: true),
                "collapse" => ExecuteExpandCollapse(element, expand: false),
                "click" => ExecuteClick(request, target.Entry.WindowRef, element),
                "press_keys" => ExecutePressKeys(request, target.Entry.WindowRef),
                _ => new ToolResult<ActionExecutionResult>(null, new ToolError(
                    ToolErrorCode.ActionNotSupported,
                    $"Action '{request.Action}' is not supported in phase 3.",
                    false,
                    "Use one of invoke, focus, set_value, range_value, select, toggle, expand, collapse, click, press_keys.",
                    new Dictionary<string, string?>()))
            };
        }
        catch (Exception ex)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.BackendError,
                ex.Message,
                true,
                "Retry the action after refreshing the target snapshot.",
                new Dictionary<string, string?>
                {
                    ["backend_used"] = "uia",
                    ["exception_type"] = ex.GetType().Name,
                }));
        }
    }

    private ToolResult<ActionExecutionResult> ExecuteInvoke(ActionRequest request, WindowRef windowRef, AutomationElement element)
    {
        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
            return Success("uia_pattern", "invoke_pattern", element);
        }

        if (!request.ExecutionPolicy.AllowFallback)
        {
            return FallbackBlocked("invoke", "click fallback is disabled by policy.");
        }

        return ExecuteClickableInput(request, windowRef, "input_simulation", "invoke_fallback_click", element);
    }

    private ToolResult<ActionExecutionResult> ExecuteFocus(ActionRequest request, WindowRef windowRef, AutomationElement element)
    {
        var activationError = EnsureForegroundIfRequested(request, windowRef);
        if (activationError is not null)
        {
            return new ToolResult<ActionExecutionResult>(null, activationError);
        }

        element.Focus();
        var focusChanged = element.Properties.HasKeyboardFocus.ValueOrDefault;
        for (var attempt = 0; !focusChanged && attempt < 4; attempt++)
        {
            Thread.Sleep(100);
            focusChanged = element.Properties.HasKeyboardFocus.ValueOrDefault;
        }

        return new ToolResult<ActionExecutionResult>(
            new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["focus_changed"] = focusChanged.ToString().ToLowerInvariant(),
                    ["target_name"] = GetOptionalProperty(() => element.Name),
                    ["target_role"] = GetOptionalProperty(() => element.ControlType.ToString()),
                },
                new Dictionary<string, string?>
                {
                    ["backend_used"] = "uia",
                    ["action_strategy"] = "focus",
                }),
            null);
    }

    private ToolResult<ActionExecutionResult> ExecuteSetValue(ActionRequest request, AutomationElement element)
    {
        if (!TryGetStringArgument(request.Arguments, "text", out var text) || text is null)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.InvalidArgument,
                "set_value requires a non-empty 'text' argument.",
                false,
                "Provide arguments.text with the desired content.",
                new Dictionary<string, string?>()));
        }

        if (element.Patterns.Value.IsSupported)
        {
            element.Patterns.Value.Pattern.SetValue(text);
            var observedText = ReadCurrentValue(element);
            for (var attempt = 0; !string.Equals(observedText, text, StringComparison.Ordinal) && attempt < 5; attempt++)
            {
                element.Patterns.Value.Pattern.SetValue(string.Empty);
                Thread.Sleep(50);
                element.Patterns.Value.Pattern.SetValue(text);
                Thread.Sleep(100);
                observedText = ReadCurrentValue(element);
            }

            var usedProtectedFallback = false;
            var protectedReadback = LooksLikeProtectedValue(observedText);
            if (protectedReadback)
            {
                observedText = text;
                usedProtectedFallback = true;
            }

            return new ToolResult<ActionExecutionResult>(
                new ActionExecutionResult(
                    true,
                    "uia_pattern",
                    new Dictionary<string, string?>
                    {
                        ["text_updated"] = observedText,
                    },
                    new Dictionary<string, string?>
                    {
                        ["backend_used"] = "uia",
                        ["action_strategy"] = "value_pattern",
                        ["value_readback"] = usedProtectedFallback ? "protected_unreadable" : "direct",
                        ["value_readback_fallback"] = usedProtectedFallback.ToString().ToLowerInvariant(),
                    }),
                null);
        }

        return new ToolResult<ActionExecutionResult>(null, new ToolError(
            ToolErrorCode.ActionNotSupported,
            "Target does not support ValuePattern for set_value.",
            false,
            "Use a different target or enable a future text-input fallback path.",
            new Dictionary<string, string?>
            {
                ["backend_used"] = "uia",
            }));
    }

    private ToolResult<ActionExecutionResult> ExecuteToggle(ActionRequest request, AutomationElement element)
    {
        if (!element.Patterns.Toggle.IsSupported)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target does not support TogglePattern.",
                false,
                "Use a control that exposes a toggle action.",
                new Dictionary<string, string?>()));
        }

        var hasTargetState = TryGetBooleanArgument(request.Arguments, "target_state", out var desiredState);
        if (hasTargetState)
        {
            var current = element.Patterns.Toggle.Pattern.ToggleState == FlaUI.Core.Definitions.ToggleState.On;
            if (current != desiredState)
            {
                element.Patterns.Toggle.Pattern.Toggle();
            }
        }
        else
        {
            element.Patterns.Toggle.Pattern.Toggle();
        }

        var isOn = element.Patterns.Toggle.Pattern.ToggleState == FlaUI.Core.Definitions.ToggleState.On;
        return new ToolResult<ActionExecutionResult>(
            new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["toggle_state"] = isOn.ToString().ToLowerInvariant(),
                },
                new Dictionary<string, string?>
                {
                    ["backend_used"] = "uia",
                    ["action_strategy"] = "toggle_pattern",
                }),
            null);
    }

    private ToolResult<ActionExecutionResult> ExecuteRangeValue(ActionRequest request, AutomationElement element)
    {
        if (!TryGetDoubleArgument(request.Arguments, "target_value", out var targetValue))
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.InvalidArgument,
                "range_value requires a numeric 'target_value' argument.",
                false,
                "Provide arguments.target_value with the desired numeric value.",
                new Dictionary<string, string?>()));
        }

        if (!element.Patterns.RangeValue.IsSupported)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target does not support RangeValuePattern.",
                false,
                "Use a control that exposes range_value or choose a different target.",
                new Dictionary<string, string?>()));
        }

        dynamic? pattern;
        try
        {
            pattern = element.Patterns.RangeValue.Pattern;
            _ = pattern.Value;
        }
        catch (PropertyNotSupportedException)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target advertises range_value but does not expose a usable RangeValuePattern at execution time.",
                false,
                "Choose a control with a stable range_value pattern or refresh the snapshot and retry on a different target.",
                new Dictionary<string, string?>()));
        }
        catch (COMException)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target advertises range_value but does not expose a usable RangeValuePattern at execution time.",
                false,
                "Choose a control with a stable range_value pattern or refresh the snapshot and retry on a different target.",
                new Dictionary<string, string?>()));
        }

        if (pattern.IsReadOnly)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target exposes RangeValuePattern but is read-only.",
                false,
                "Choose a writable range control or a different target.",
                new Dictionary<string, string?>()));
        }

        var minimum = (double)pattern.Minimum;
        var maximum = (double)pattern.Maximum;
        var currentValue = (double)pattern.Value;
        var clampedValue = Math.Min(maximum, Math.Max(minimum, targetValue));

        pattern.SetValue(clampedValue);
        var observedValue = (double)pattern.Value;
        for (var attempt = 0; Math.Abs(observedValue - clampedValue) > 0.001d && attempt < 5; attempt++)
        {
            Thread.Sleep(100);
            pattern.SetValue(clampedValue);
            observedValue = (double)pattern.Value;
        }

        return new ToolResult<ActionExecutionResult>(
            new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["range_value"] = FormatDouble(observedValue),
                    ["previous_range_value"] = FormatDouble(currentValue),
                    ["target_range_value"] = FormatDouble(clampedValue),
                    ["range_minimum"] = FormatDouble(minimum),
                    ["range_maximum"] = FormatDouble(maximum),
                },
                new Dictionary<string, string?>
                {
                    ["backend_used"] = "uia",
                    ["action_strategy"] = "range_value_pattern",
                    ["range_clamped"] = (!AreClose(clampedValue, targetValue)).ToString().ToLowerInvariant(),
                }),
            null);
    }

    private ToolResult<ActionExecutionResult> ExecuteSelect(ActionRequest request, AutomationElement element)
    {
        if (!TryGetStringArgument(request.Arguments, "option_text", out var optionText) || optionText is null)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.InvalidArgument,
                "select requires a non-empty 'option_text' argument.",
                false,
                "Provide arguments.option_text with the desired option label.",
                new Dictionary<string, string?>()));
        }

        var previousSelection = ReadSelectedItem(element);
        var candidate = FindSelectionCandidate(element, optionText);
        if (candidate is null)
        {
            TryExpandForSelection(element);
            candidate = FindSelectionCandidate(element, optionText);
        }

        if (candidate is null)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.TargetNotFound,
                $"No selectable option matched '{optionText}'.",
                true,
                "Refresh the snapshot and retry with a visible option label.",
                new Dictionary<string, string?>
                {
                    ["requested_option_text"] = optionText,
                }));
        }

        if (!candidate.Patterns.SelectionItem.IsSupported)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Matched option does not support SelectionItemPattern.",
                false,
                "Choose a control that exposes selectable items or refresh the snapshot and retry.",
                new Dictionary<string, string?>
                {
                    ["requested_option_text"] = optionText,
                }));
        }

        try
        {
            candidate.Patterns.SelectionItem.Pattern.Select();
        }
        catch (PropertyNotSupportedException)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Matched option advertises selection but does not expose a usable SelectionItemPattern at execution time.",
                false,
                "Choose a control with a stable selection pattern or refresh the snapshot and retry on a different target.",
                new Dictionary<string, string?>
                {
                    ["requested_option_text"] = optionText,
                }));
        }
        catch (COMException)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Matched option advertises selection but does not expose a usable SelectionItemPattern at execution time.",
                false,
                "Choose a control with a stable selection pattern or refresh the snapshot and retry on a different target.",
                new Dictionary<string, string?>
                {
                    ["requested_option_text"] = optionText,
                }));
        }

        var observedSelection = ReadSelectedItem(element);
        for (var attempt = 0; !string.Equals(observedSelection, optionText, StringComparison.Ordinal) && attempt < 5; attempt++)
        {
            Thread.Sleep(100);
            observedSelection = ReadSelectedItem(element);
        }

        return new ToolResult<ActionExecutionResult>(
            new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["selected_item"] = observedSelection,
                    ["previous_selected_item"] = previousSelection,
                    ["target_selected_item"] = optionText,
                },
                new Dictionary<string, string?>
                {
                    ["backend_used"] = "uia",
                    ["action_strategy"] = "selection_item_pattern",
                }),
            null);
    }

    private ToolResult<ActionExecutionResult> ExecuteExpandCollapse(AutomationElement element, bool expand)
    {
        if (!element.Patterns.ExpandCollapse.IsSupported)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target does not support ExpandCollapsePattern.",
                false,
                "Use a control that exposes expand/collapse.",
                new Dictionary<string, string?>()));
        }

        dynamic? pattern;
        try
        {
            pattern = element.Patterns.ExpandCollapse.Pattern;
            _ = pattern.ExpandCollapseState;
        }
        catch (PropertyNotSupportedException)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target advertises expand/collapse but does not expose a usable ExpandCollapsePattern at execution time.",
                false,
                "Choose a control that exposes a stable expand/collapse pattern or refresh the snapshot and retry on a different target.",
                new Dictionary<string, string?>()));
        }
        catch (COMException)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.ActionNotSupported,
                "Target advertises expand/collapse but does not expose a usable ExpandCollapsePattern at execution time.",
                false,
                "Choose a control that exposes a stable expand/collapse pattern or refresh the snapshot and retry on a different target.",
                new Dictionary<string, string?>()));
        }

        var expectedState = expand ? "Expanded" : "Collapsed";
        if (expand)
        {
            pattern.Expand();
        }
        else
        {
            pattern.Collapse();
        }

        var observedState = pattern.ExpandCollapseState.ToString();
        for (var attempt = 0; !string.Equals(observedState, expectedState, StringComparison.OrdinalIgnoreCase) && attempt < 5; attempt++)
        {
            if (expand)
            {
                pattern.Expand();
            }
            else
            {
                pattern.Collapse();
            }
            Thread.Sleep(100);
            observedState = pattern.ExpandCollapseState.ToString();
        }

        return new ToolResult<ActionExecutionResult>(
            new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["expand_state"] = observedState,
                },
                new Dictionary<string, string?>
                {
                    ["backend_used"] = "uia",
                    ["action_strategy"] = expand ? "expand_pattern" : "collapse_pattern",
                }),
            null);
    }

    private ToolResult<ActionExecutionResult> ExecuteClick(ActionRequest request, WindowRef windowRef, AutomationElement element)
    {
        if (element.Patterns.Invoke.IsSupported)
        {
            element.Patterns.Invoke.Pattern.Invoke();
            return Success("uia_pattern", "semantic_click", element);
        }

        if (!request.ExecutionPolicy.AllowFallback)
        {
            return FallbackBlocked("click", "input simulation fallback is disabled by policy.");
        }

        return ExecuteClickableInput(request, windowRef, "input_simulation", "mouse_click", element);
    }

    private ToolResult<ActionExecutionResult> ExecutePressKeys(ActionRequest request, WindowRef windowRef)
    {
        if (!TryGetKeys(request.Arguments, out var chord))
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.InvalidArgument,
                "press_keys requires a non-empty keys array.",
                false,
                "Provide arguments.keys like ['CTRL', 'S'].",
                new Dictionary<string, string?>()));
        }

        var activationError = EnsureForegroundIfRequested(request, windowRef);
        if (activationError is not null)
        {
            return new ToolResult<ActionExecutionResult>(null, activationError);
        }

        Keyboard.TypeSimultaneously(chord);
        return new ToolResult<ActionExecutionResult>(
            new ActionExecutionResult(
                true,
                "input_simulation",
                new Dictionary<string, string?>
                {
                    ["keys_sent"] = string.Join("+", chord.Select(key => key.ToString())),
                },
                new Dictionary<string, string?>
                {
                    ["backend_used"] = "input_simulation",
                    ["action_strategy"] = "keyboard_shortcut",
                }),
            null);
    }

    private ToolResult<ActionExecutionResult> ExecuteClickableInput(ActionRequest request, WindowRef windowRef, string executionPath, string strategy, AutomationElement element)
    {
        var activationError = EnsureForegroundIfRequested(request, windowRef);
        if (activationError is not null)
        {
            return new ToolResult<ActionExecutionResult>(null, activationError);
        }

        var bounds = element.BoundingRectangle;
        if (bounds.IsEmpty)
        {
            return new ToolResult<ActionExecutionResult>(null, new ToolError(
                ToolErrorCode.TargetNotActionable,
                "Target does not expose a reliable bounding rectangle for input simulation.",
                false,
                "Choose a more actionable target or rely on a supported UIA pattern.",
                new Dictionary<string, string?>()));
        }

        Mouse.MoveTo(new System.Drawing.Point(
            (int)(bounds.Left + (bounds.Width / 2)),
            (int)(bounds.Top + (bounds.Height / 2))));
        Mouse.Click();
        return Success(executionPath, strategy, element);
    }

    private ToolError? EnsureWindowActive(WindowRef windowRef)
    {
        var handle = _windowRegistry.GetNativeHandle(windowRef);
        if (handle is null || handle == nint.Zero)
        {
            return new ToolError(
                ToolErrorCode.WindowNotFound,
                $"Unable to resolve native window handle for {windowRef.Value}.",
                true,
                "Refresh the window list and retry.",
                new Dictionary<string, string?>());
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            NativeMethods.SetForegroundWindow(handle.Value);
            Thread.Sleep(100);

            var activeHandle = NativeMethods.GetForegroundWindow();
            if (activeHandle == handle.Value)
            {
                return null;
            }
        }

        if (NativeMethods.GetForegroundWindow() != handle.Value)
        {
            return new ToolError(
                ToolErrorCode.WindowNotActive,
                $"Window {windowRef.Value} could not be activated before action execution.",
                true,
                "Bring the target window to the foreground and retry.",
                new Dictionary<string, string?>());
        }

        return null;
    }

    private ToolError? EnsureForegroundIfRequested(ActionRequest request, WindowRef windowRef)
    {
        return request.ExecutionPolicy.AutoActivateWindow
            ? EnsureWindowActive(windowRef)
            : null;
    }

    private static ToolResult<ActionExecutionResult> Success(string executionPath, string strategy, AutomationElement element)
    {
        return new ToolResult<ActionExecutionResult>(
            new ActionExecutionResult(
                true,
                executionPath,
                new Dictionary<string, string?>
                {
                    ["target_name"] = GetOptionalProperty(() => element.Name),
                    ["target_role"] = GetOptionalProperty(() => element.ControlType.ToString()),
                },
                new Dictionary<string, string?>
                {
                    ["backend_used"] = executionPath == "input_simulation" ? "input_simulation" : "uia",
                    ["action_strategy"] = strategy,
                }),
            null);
    }

    private static ToolResult<ActionExecutionResult> FallbackBlocked(string action, string message)
    {
        return new ToolResult<ActionExecutionResult>(null, new ToolError(
            ToolErrorCode.FallbackBlocked,
            $"Action '{action}' requires fallback, but fallback is blocked. {message}",
            false,
            "Allow fallback in execution policy or choose a target with native UIA support.",
            new Dictionary<string, string?>()));
    }

    private static bool TryGetStringArgument(IReadOnlyDictionary<string, object?> arguments, string key, out string? value)
    {
        if (arguments.TryGetValue(key, out var raw))
        {
            value = raw switch
            {
                string text => text,
                _ => raw?.ToString(),
            };

            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }

    private static bool TryGetBooleanArgument(IReadOnlyDictionary<string, object?> arguments, string key, out bool value)
    {
        if (!arguments.TryGetValue(key, out var raw) || raw is null)
        {
            value = false;
            return false;
        }

        switch (raw)
        {
            case bool boolean:
                value = boolean;
                return true;
            case string text when bool.TryParse(text, out var parsed):
                value = parsed;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private static bool TryGetDoubleArgument(IReadOnlyDictionary<string, object?> arguments, string key, out double value)
    {
        if (!arguments.TryGetValue(key, out var raw) || raw is null)
        {
            value = default;
            return false;
        }

        switch (raw)
        {
            case double doubleValue:
                value = doubleValue;
                return true;
            case float floatValue:
                value = floatValue;
                return true;
            case decimal decimalValue:
                value = (double)decimalValue;
                return true;
            case int intValue:
                value = intValue;
                return true;
            case long longValue:
                value = longValue;
                return true;
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedInvariant):
                value = parsedInvariant;
                return true;
            case string text when double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsedCurrent):
                value = parsedCurrent;
                return true;
            default:
                value = default;
                return false;
        }
    }

    private static bool LooksLikeProtectedValue(string? value)
    {
        return string.Equals(value, "Access denied", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "已拒绝访问", StringComparison.Ordinal)
            || string.Equals(value, "拒绝访问", StringComparison.Ordinal);
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

    private static string? ReadCurrentValue(AutomationElement element)
    {
        return element.Patterns.Value.Pattern.Value.ValueOrDefault;
    }

    private static AutomationElement? FindSelectionCandidate(AutomationElement element, string optionText)
    {
        if (element.Patterns.SelectionItem.IsSupported)
        {
            var elementName = GetOptionalProperty(() => element.Name);
            if (string.Equals(elementName, optionText, StringComparison.Ordinal))
            {
                return element;
            }

            if (string.Equals(elementName, optionText, StringComparison.OrdinalIgnoreCase))
            {
                return element;
            }
        }

        var descendants = element.FindAllDescendants();
        var exact = descendants.FirstOrDefault(candidate =>
            candidate.Patterns.SelectionItem.IsSupported &&
            string.Equals(GetOptionalProperty(() => candidate.Name), optionText, StringComparison.Ordinal));

        if (exact is not null)
        {
            return exact;
        }

        return descendants.FirstOrDefault(candidate =>
            candidate.Patterns.SelectionItem.IsSupported &&
            string.Equals(GetOptionalProperty(() => candidate.Name), optionText, StringComparison.OrdinalIgnoreCase));
    }

    private static void TryExpandForSelection(AutomationElement element)
    {
        try
        {
            if (!element.Patterns.ExpandCollapse.IsSupported)
            {
                return;
            }

            var pattern = element.Patterns.ExpandCollapse.Pattern;
            var state = pattern.ExpandCollapseState;
            if (state.ToString().Contains("Collapsed", StringComparison.OrdinalIgnoreCase))
            {
                pattern.Expand();
                Thread.Sleep(150);
            }
        }
        catch (PropertyNotSupportedException)
        {
        }
        catch (COMException)
        {
        }
    }

    private static string? ReadSelectedItem(AutomationElement element)
    {
        try
        {
            if (element.Patterns.SelectionItem.IsSupported)
            {
                try
                {
                    if (element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault)
                    {
                        return GetOptionalProperty(() => element.Name);
                    }
                }
                catch (PropertyNotSupportedException)
                {
                }
                catch (COMException)
                {
                }
            }

            if (element.Patterns.Selection.IsSupported)
            {
                var selectedItems = element.Patterns.Selection.Pattern.Selection.ValueOrDefault;
                var selectedItem = selectedItems?.FirstOrDefault();
                if (selectedItem is not null)
                {
                    return GetOptionalProperty(() => selectedItem.Name);
                }
            }

            var selectedDescendant = element.FindAllDescendants()
                .FirstOrDefault(candidate =>
                {
                    try
                    {
                        return candidate.Patterns.SelectionItem.IsSupported &&
                               candidate.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault;
                    }
                    catch (PropertyNotSupportedException)
                    {
                        return false;
                    }
                    catch (COMException)
                    {
                        return false;
                    }
                });

            return selectedDescendant is null ? null : GetOptionalProperty(() => selectedDescendant.Name);
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

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) <= 0.001d;
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static bool TryGetKeys(IReadOnlyDictionary<string, object?> arguments, out VirtualKeyShort[] chord)
    {
        chord = Array.Empty<VirtualKeyShort>();
        if (!arguments.TryGetValue("keys", out var raw) || raw is not IEnumerable<object?> values)
        {
            return false;
        }

        var resolved = new List<VirtualKeyShort>();
        foreach (var value in values)
        {
            if (value is null)
            {
                continue;
            }

            if (!TryMapKey(value.ToString()!, out var key))
            {
                return false;
            }

            resolved.Add(key);
        }

        chord = resolved.ToArray();
        return chord.Length > 0;
    }

    private static bool TryMapKey(string value, out VirtualKeyShort key)
    {
        switch (value.Trim().ToUpperInvariant())
        {
            case "CTRL":
            case "CONTROL":
                key = VirtualKeyShort.CONTROL;
                return true;
            case "SHIFT":
                key = VirtualKeyShort.SHIFT;
                return true;
            case "ALT":
                key = VirtualKeyShort.LMENU;
                return true;
            case "ENTER":
                key = VirtualKeyShort.RETURN;
                return true;
            case "ESC":
            case "ESCAPE":
                key = VirtualKeyShort.ESCAPE;
                return true;
            case "TAB":
                key = VirtualKeyShort.TAB;
                return true;
            case "SPACE":
                key = VirtualKeyShort.SPACE;
                return true;
        }

        if (value.Length is 2 or 3
            && value[0] is 'F' or 'f'
            && int.TryParse(value[1..], out var functionKeyNumber)
            && functionKeyNumber is >= 1 and <= 24)
        {
            key = (VirtualKeyShort)((int)VirtualKeyShort.F1 + (functionKeyNumber - 1));
            return true;
        }

        if (value.Length == 1)
        {
            var character = char.ToUpperInvariant(value[0]);
            if (character is >= 'A' and <= 'Z')
            {
                key = (VirtualKeyShort)character;
                return true;
            }

            if (character is >= '0' and <= '9')
            {
                key = (VirtualKeyShort)character;
                return true;
            }
        }

        key = default;
        return false;
    }
}
