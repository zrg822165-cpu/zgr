using OpenClaw.Core.Actions;

namespace OpenClaw.Protocol.Actions;

internal static class ActionDiagnostics
{
    public static IReadOnlyDictionary<string, string?> ForError(
        string toolName,
        ActionRequest request,
        TargetResolution? resolution,
        string? executionPath,
        IReadOnlyDictionary<string, string?>? diagnostics,
        params IReadOnlyDictionary<string, string?>?[] extras)
    {
        return ForAction(toolName, request, resolution, executionPath, diagnostics, extras);
    }

    public static IReadOnlyDictionary<string, string?> ForAction(
        string toolName,
        ActionRequest request,
        TargetResolution? resolution,
        string? executionPath,
        IReadOnlyDictionary<string, string?>? diagnostics,
        params IReadOnlyDictionary<string, string?>?[] extras)
    {
        var groups = new List<IReadOnlyDictionary<string, string?>?>
        {
            diagnostics,
            new Dictionary<string, string?>
            {
                ["tool_name"] = toolName,
                ["interaction_model"] = "text_structured",
                ["primary_interface"] = "accessibility_tree",
                ["action_name"] = request.Action,
                ["target_ref"] = resolution?.MatchedRef?.Value ?? request.Target.Ref,
                ["window_ref"] = resolution?.WindowRef?.Value,
                ["target_source"] = resolution?.ResolutionSource ?? InferTargetSource(request.Target),
                ["selector_used"] = resolution?.SelectorUsed ?? request.Target.Selector,
                ["execution_path"] = executionPath,
            }
        };

        groups.AddRange(extras);
        return Merge(groups.ToArray());
    }

    public static IReadOnlyDictionary<string, string?> Merge(params IReadOnlyDictionary<string, string?>?[] groups)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            if (group is null)
            {
                continue;
            }

            foreach (var pair in group)
            {
                merged[pair.Key] = pair.Value;
            }
        }

        return merged;
    }

    private static string? InferTargetSource(ActionTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.Ref))
        {
            return "ref";
        }

        return string.IsNullOrWhiteSpace(target.Selector)
            ? null
            : "selector";
    }
}
