using A11yFlow.Core.Abstractions;
using A11yFlow.Core.Errors;
using A11yFlow.Core.Locators;
using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;
using A11yFlow.Core.Snapshots;

namespace A11yFlow.Core.Actions;

public sealed class TargetResolver : ITargetResolver
{
    private readonly IRefRegistry _refRegistry;
    private readonly IWindowRegistry _windowRegistry;
    private readonly ISnapshotBuilder _snapshotBuilder;
    private readonly SelectorParser _selectorParser = new();
    private readonly SnapshotLocator _locator = new();

    public TargetResolver(IRefRegistry refRegistry, IWindowRegistry windowRegistry, ISnapshotBuilder snapshotBuilder)
    {
        _refRegistry = refRegistry;
        _windowRegistry = windowRegistry;
        _snapshotBuilder = snapshotBuilder;
    }

    public ToolResult<ResolvedTarget> Resolve(ActionTarget target)
    {
        if (!string.IsNullOrWhiteSpace(target.Ref))
        {
            var byRef = ResolveByRef(target.Ref!, target.Selector);
            if (byRef.IsSuccess)
            {
                return byRef;
            }

            var fallbackSelector = ResolveFallbackSelector(target.Ref!, target.Selector);
            if (byRef.Error?.Code != ToolErrorCode.TargetStale || string.IsNullOrWhiteSpace(fallbackSelector))
            {
                return byRef;
            }

            return ResolveBySelector(fallbackSelector!, target.Ref, selectorSource: target.Selector is null ? "selector_hint" : "request_selector");
        }

        if (!string.IsNullOrWhiteSpace(target.Selector))
        {
            return ResolveBySelector(target.Selector!, null, selectorSource: "request_selector");
        }

        return new ToolResult<ResolvedTarget>(null, new ToolError(
            ToolErrorCode.InvalidArgument,
            "Action target requires either ref or selector.",
            false,
            "Provide a target ref from snapshot output or a selector for locate.",
            new Dictionary<string, string?>()));
    }

    private ToolResult<ResolvedTarget> ResolveByRef(string reference, string? selectorHint)
    {
        if (!_refRegistry.TryGetEntry(reference, out var entry) || entry is null)
        {
            return new ToolResult<ResolvedTarget>(null, new ToolError(
                ToolErrorCode.RefNotFound,
                $"Ref '{reference}' is unknown in the current session.",
                true,
                selectorHint is null ? "Take a fresh snapshot and retry." : "Retry with the selector fallback or take a fresh snapshot.",
                ToolRecoverySemantics.For(ToolErrorCode.RefNotFound)));
        }

        if (entry.AutomationElement is null)
        {
            return new ToolResult<ResolvedTarget>(null, new ToolError(
                ToolErrorCode.TargetStale,
                $"Ref '{reference}' no longer points to a live automation element.",
                true,
                "Use the selector fallback or refresh the snapshot.",
                MergeDiagnostics(
                    ToolRecoverySemantics.For(ToolErrorCode.TargetStale),
                    new Dictionary<string, string?>
                    {
                        ["ref"] = reference,
                        ["snapshot_version"] = entry.SnapshotVersion,
                    })));
        }

        return new ToolResult<ResolvedTarget>(
            new ResolvedTarget(
                entry,
                null,
                new TargetResolution(
                    "ref",
                    new ElementRef(entry.Ref),
                    entry.WindowRef,
                    selectorHint,
                    1.0d,
                    new Dictionary<string, string?>
                    {
                        ["snapshot_version"] = entry.SnapshotVersion,
                        ["backend_used"] = entry.BackendSource,
                        ["selector_hint"] = entry.SelectorHint,
                    })),
            null);
    }

    private ToolResult<ResolvedTarget> ResolveBySelector(string selector, string? requestedRef, string selectorSource)
    {
        var parseResult = _selectorParser.Parse(selector);
        if (!parseResult.IsSuccess)
        {
            return new ToolResult<ResolvedTarget>(null, new ToolError(
                ToolErrorCode.InvalidArgument,
                parseResult.Error!.Message,
                false,
                "Fix the selector syntax and retry.",
                BuildSelectorErrorDiagnostics(
                    selector,
                    null,
                    new Dictionary<string, string?>
                    {
                        ["error_position"] = parseResult.Error.Position.ToString(),
                    })));
        }

        var snapshotResult = ResolveScopedSnapshot(parseResult.Alternatives[0].Scope);
        if (!snapshotResult.IsSuccess)
        {
            return new ToolResult<ResolvedTarget>(null, snapshotResult.Error);
        }

        var snapshot = snapshotResult.Payload!;
        var located = _locator.Locate(snapshot, parseResult);
        if (located.Status == LocateStatus.NotFound)
        {
            return new ToolResult<ResolvedTarget>(null, new ToolError(
                ToolErrorCode.TargetNotFound,
                located.Explain,
                true,
                "Refresh the snapshot or relax the selector.",
                BuildSelectorErrorDiagnostics(
                    selector,
                    snapshot.WindowRef.Value,
                    MergeDiagnostics(ToolRecoverySemantics.For(ToolErrorCode.TargetNotFound), located.Diagnostics))));
        }

        if (located.Status == LocateStatus.Ambiguous)
        {
            return new ToolResult<ResolvedTarget>(null, new ToolError(
                ToolErrorCode.TargetAmbiguous,
                located.Explain,
                false,
                BuildAmbiguousSelectorNextStep(located),
                BuildAmbiguousSelectorDiagnostics(selector, snapshot.WindowRef.Value, located)));
        }

        if (located.BestMatch is null || !_refRegistry.TryGetEntry(located.BestMatch.Ref.Value, out var entry) || entry is null)
        {
            return new ToolResult<ResolvedTarget>(null, new ToolError(
                ToolErrorCode.TargetStale,
                "The selector matched an element, but its live automation reference is unavailable.",
                true,
                "Take a fresh snapshot and retry the action.",
                BuildSelectorErrorDiagnostics(
                    selector,
                    snapshot.WindowRef.Value,
                    MergeDiagnostics(ToolRecoverySemantics.For(ToolErrorCode.TargetStale), located.Diagnostics))));
        }

        return new ToolResult<ResolvedTarget>(
            new ResolvedTarget(
                entry,
                snapshot,
                new TargetResolution(
                    requestedRef is null ? "selector" : "selector_recovery",
                    located.BestMatch.Ref,
                    snapshot.WindowRef,
                    selector,
                    located.BestMatch.Confidence,
                    MergeDiagnostics(
                        located.Diagnostics,
                        new Dictionary<string, string?>
                        {
                            ["selector_source"] = selectorSource,
                            ["requested_ref"] = requestedRef,
                            ["resolved_ref"] = entry.Ref,
                            ["ref_changed"] = requestedRef is null ? "false" : (!string.Equals(requestedRef, entry.Ref, StringComparison.Ordinal)).ToString().ToLowerInvariant(),
                            ["recovered_from_stale_ref"] = (requestedRef is not null).ToString().ToLowerInvariant(),
                            ["snapshot_version"] = entry.SnapshotVersion,
                            ["backend_used"] = entry.BackendSource,
                        }))),
            null);
    }

    private static IReadOnlyDictionary<string, string?> BuildSelectorErrorDiagnostics(
        string selector,
        string? windowRef,
        IReadOnlyDictionary<string, string?> diagnostics)
    {
        return MergeDiagnostics(
            diagnostics,
            new Dictionary<string, string?>
            {
                ["tool_name"] = "windows_locate",
                ["query_kind"] = "windows_locate",
                ["interaction_model"] = "text_structured",
                ["primary_interface"] = "accessibility_tree",
                ["selector_used"] = selector,
                ["window_ref"] = windowRef,
            });
    }

    private static string BuildAmbiguousSelectorNextStep(LocateResult located)
    {
        var refsPreview = string.Join(", ", located.Candidates.Take(3).Select(candidate => candidate.Ref.Value));
        return string.IsNullOrWhiteSpace(refsPreview)
            ? "Use windows_describe_ref on the ambiguous candidate refs, then refine the selector by role, automation_id, name, or structure before retrying the action."
            : $"Use windows_describe_ref on ambiguous candidate refs like {refsPreview}, then refine the selector by role, automation_id, name, or structure before retrying the action.";
    }

    private static IReadOnlyDictionary<string, string?> BuildAmbiguousSelectorDiagnostics(
        string selector,
        string? windowRef,
        LocateResult located)
    {
        return BuildSelectorErrorDiagnostics(
            selector,
            windowRef,
            MergeDiagnostics(
                MergeDiagnostics(
                    located.Diagnostics,
                    new Dictionary<string, string?>
                    {
                        ["candidate_refs_preview"] = string.Join(",", located.Candidates.Take(5).Select(candidate => candidate.Ref.Value)),
                        ["candidate_roles_preview"] = string.Join(",", located.Candidates.Take(5).Select(candidate => candidate.Role)),
                        ["candidate_names_preview"] = string.Join(" | ", located.Candidates.Take(5).Select(candidate => candidate.Name ?? string.Empty)),
                    }),
                ToolRecoverySemantics.For(ToolErrorCode.TargetAmbiguous)));
    }

    private string? ResolveFallbackSelector(string requestedRef, string? requestSelector)
    {
        if (!string.IsNullOrWhiteSpace(requestSelector))
        {
            return requestSelector;
        }

        if (_refRegistry.TryGetEntry(requestedRef, out var entry) && entry is not null && !string.IsNullOrWhiteSpace(entry.SelectorHint))
        {
            return entry.SelectorHint;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string?> MergeDiagnostics(
        IReadOnlyDictionary<string, string?> primary,
        IReadOnlyDictionary<string, string?> secondary)
    {
        var merged = new Dictionary<string, string?>(primary);
        foreach (var pair in secondary)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private ToolResult<SnapshotResult> ResolveScopedSnapshot(SelectorScope scope)
    {
        if (scope.Kind == SelectorScopeKind.ActiveWindow)
        {
            try
            {
                return new ToolResult<SnapshotResult>(_snapshotBuilder.BuildActiveWindowSnapshot(), null);
            }
            catch (InvalidOperationException ex)
            {
                return new ToolResult<SnapshotResult>(null, new ToolError(
                    ToolErrorCode.NoActiveWindow,
                    ex.Message,
                    true,
                    "Activate a target window and retry.",
                    ToolRecoverySemantics.For(ToolErrorCode.NoActiveWindow)));
            }
        }

        var window = _windowRegistry.ListWindows().FirstOrDefault(candidate =>
            candidate.Title.Contains(scope.WindowName ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (window is null)
        {
            return new ToolResult<SnapshotResult>(null, new ToolError(
                ToolErrorCode.WindowNotFound,
                $"No window matched '{scope.WindowName}'.",
                true,
                "Use windows_list to inspect current top-level windows.",
                MergeDiagnostics(
                    ToolRecoverySemantics.For(ToolErrorCode.WindowNotFound),
                    new Dictionary<string, string?>
                    {
                        ["scope_name"] = scope.WindowName,
                    })));
        }

        try
        {
            return new ToolResult<SnapshotResult>(_snapshotBuilder.BuildWindowSnapshot(window.Ref), null);
        }
        catch (InvalidOperationException ex)
        {
            return new ToolResult<SnapshotResult>(null, new ToolError(
                ToolErrorCode.WindowNotFound,
                ex.Message,
                true,
                "Refresh the window list and request a new snapshot.",
                ToolRecoverySemantics.For(ToolErrorCode.WindowNotFound)));
        }
    }
}
