using OpenClaw.Core.Abstractions;
using OpenClaw.Core.Errors;
using OpenClaw.Core.Locators;
using OpenClaw.Core.Models;

namespace OpenClaw.Protocol.Queries;

public sealed class QueryToolService
{
    private readonly SnapshotLocator _locator;
    private readonly SelectorParser _selectorParser;
    private readonly IWindowRegistry _windowRegistry;
    private readonly ISnapshotBuilder _snapshotBuilder;
    private const int RecentSnapshotLimit = 4;
    private OpenClaw.Core.Snapshots.SnapshotResult? _activeWindowSnapshot;
    private OpenClaw.Core.Snapshots.SnapshotResult? _focusContextSnapshot;
    private readonly LinkedList<OpenClaw.Core.Snapshots.SnapshotResult> _recentWindowSnapshots = [];
    private string? _recentLocatorSelector;
    private string? _recentLocatorRef;

    public QueryToolService(IWindowRegistry windowRegistry, ISnapshotBuilder snapshotBuilder)
    {
        _locator = new SnapshotLocator();
        _selectorParser = new SelectorParser();
        _windowRegistry = windowRegistry;
        _snapshotBuilder = snapshotBuilder;
    }

    public WindowsListResult WindowsList()
    {
        var windows = _windowRegistry.ListWindows();
        return new WindowsListResult(
            windows,
            windows.Count,
            windows.FirstOrDefault(window => window.IsActive)?.Ref,
            QueryDiagnostics.ForWindowsList(
                windows.Count,
                windows.FirstOrDefault(window => window.IsActive)?.Ref));
    }

    public ToolResult<ActiveWindowResult> WindowsActive()
    {
        var window = _windowRegistry.GetActiveWindow();
        if (window is null)
        {
            return new ToolResult<ActiveWindowResult>(null, new ToolError(
                ToolErrorCode.NoActiveWindow,
                "No active window was detected.",
                true,
                "Activate a target window and retry.",
                QueryDiagnostics.ForError("windows_active", null, ToolRecoverySemantics.For(ToolErrorCode.NoActiveWindow))));
        }

        var snapshot = GetSnapshot(window.Ref, preferActiveCache: true);
        var actionableSummary = Flatten(snapshot.Root)
            .Where(node => node.Actions.Count > 0)
            .Select(node => $"{node.Role}:{node.Name ?? string.Empty}")
            .Take(20)
            .ToList();

        return new ToolResult<ActiveWindowResult>(
            new ActiveWindowResult(
                window,
                FindFocusedRef(snapshot.Root),
                actionableSummary,
                snapshot.SummaryText,
                QueryDiagnostics.ForSnapshot(
                    "windows_active",
                    snapshot,
                    "active_window_snapshot",
                    _recentLocatorSelector,
                    _recentLocatorRef,
                    new Dictionary<string, string?>
                    {
                        ["focus_ref"] = FindFocusedRef(snapshot.Root)?.Value,
                        ["actionable_count"] = actionableSummary.Count.ToString(),
                    })),
            null);
    }

    public ToolResult<OpenClaw.Core.Snapshots.SnapshotResult> WindowsSnapshot(WindowsSnapshotRequest request)
    {
        try
        {
            var snapshot = request.WindowRef is null
                ? GetActiveSnapshot()
                : GetSnapshot(request.WindowRef, preferActiveCache: false);

            RememberSnapshot(snapshot);

            var diagnostics = QueryDiagnostics.ForSnapshot(
                "windows_snapshot",
                snapshot,
                request.WindowRef is null ? "active_window_snapshot" : "fresh",
                _recentLocatorSelector,
                _recentLocatorRef);

            var normalizedSnapshot = new OpenClaw.Core.Snapshots.SnapshotResult(
                snapshot.WindowRef,
                snapshot.SnapshotVersion,
                snapshot.Root,
                snapshot.SummaryText,
                diagnostics);

            return new ToolResult<OpenClaw.Core.Snapshots.SnapshotResult>(normalizedSnapshot, null);
        }
        catch (InvalidOperationException ex)
        {
            return new ToolResult<OpenClaw.Core.Snapshots.SnapshotResult>(null, new ToolError(
                ToolErrorCode.WindowNotFound,
                ex.Message,
                false,
                "Refresh the window list and request a new snapshot.",
                QueryDiagnostics.ForError("windows_snapshot", null, ToolRecoverySemantics.For(ToolErrorCode.WindowNotFound))));
        }
    }

    public ToolResult<LocateResult> WindowsLocate(WindowsLocateRequest request)
    {
        var parseResult = _selectorParser.Parse(request.Selector);
        if (!parseResult.IsSuccess)
        {
            return new ToolResult<LocateResult>(
                new LocateResult(
                    LocateStatus.InvalidSelector,
                    null,
                    Array.Empty<ElementCandidate>(),
                    parseResult.Error!.Message,
                    new Dictionary<string, string?>
                    {
                        ["error_position"] = parseResult.Error.Position.ToString(),
                    }),
                new ToolError(
                    ToolErrorCode.InvalidArgument,
                    parseResult.Error.Message,
                    false,
                    "Fix the selector syntax and retry.",
                    QueryDiagnostics.ForError(
                        "windows_locate",
                        null,
                        new Dictionary<string, string?>
                        {
                            ["error_position"] = parseResult.Error.Position.ToString(),
                            ["selector_used"] = request.Selector,
                        })));
        }

        var alternative = parseResult.Alternatives[0];
        var snapshotResult = ResolveScopedSnapshot(alternative.Scope);
        if (!snapshotResult.IsSuccess)
        {
            return new ToolResult<LocateResult>(null, snapshotResult.Error);
        }

        var snapshot = snapshotResult.Payload!;
        RememberSnapshot(snapshot);
        var located = _locator.Locate(snapshot, parseResult);
        RememberLocate(request.Selector, located.BestMatch?.Ref.Value);

        var normalizedLocated = new LocateResult(
            located.Status,
            located.BestMatch,
            located.Candidates,
            located.Explain,
            QueryDiagnostics.ForLocate(snapshot, request.Selector, located, _recentLocatorSelector, _recentLocatorRef));

        return normalizedLocated.Status switch
        {
            LocateStatus.Found => new ToolResult<LocateResult>(normalizedLocated, null),
            LocateStatus.Ambiguous => new ToolResult<LocateResult>(normalizedLocated, new ToolError(
                ToolErrorCode.TargetAmbiguous,
                normalizedLocated.Explain,
                false,
                BuildAmbiguousLocateNextStep(normalizedLocated),
                BuildAmbiguousLocateDiagnostics(normalizedLocated))),
            LocateStatus.NotFound => new ToolResult<LocateResult>(normalizedLocated, new ToolError(
                ToolErrorCode.TargetNotFound,
                normalizedLocated.Explain,
                true,
                "Refresh the snapshot or relax the selector.",
                QueryDiagnostics.Merge(normalizedLocated.Diagnostics, ToolRecoverySemantics.For(ToolErrorCode.TargetNotFound)))),
            _ => new ToolResult<LocateResult>(normalizedLocated, new ToolError(
                ToolErrorCode.InvalidArgument,
                normalizedLocated.Explain,
                false,
                "Fix the selector syntax and retry.",
                normalizedLocated.Diagnostics)),
        };
    }

    private static string BuildAmbiguousLocateNextStep(LocateResult result)
    {
        var refsPreview = string.Join(", ", result.Candidates.Take(3).Select(candidate => candidate.Ref.Value));
        return string.IsNullOrWhiteSpace(refsPreview)
            ? "Use windows_describe_ref on the returned candidate refs, then refine the selector by role, automation_id, name, or structure."
            : $"Use windows_describe_ref on candidate refs like {refsPreview}, then refine the selector by role, automation_id, name, or structure.";
    }

    private static IReadOnlyDictionary<string, string?> BuildAmbiguousLocateDiagnostics(LocateResult result)
    {
        var diagnostics = new Dictionary<string, string?>(result.Diagnostics)
        {
            ["recovery_kind"] = "ambiguity_resolution",
            ["recovery_hint"] = "describe_candidates_then_refine",
            ["recovery_target_tool"] = "windows_describe_ref",
            ["recovery_selector_refinement"] = "role|automation_id|name|structure",
            ["suggested_next_step_kind"] = "describe_candidates_then_refine",
            ["candidate_refs_preview"] = string.Join(",", result.Candidates.Take(5).Select(candidate => candidate.Ref.Value)),
            ["candidate_roles_preview"] = string.Join(",", result.Candidates.Take(5).Select(candidate => candidate.Role)),
            ["candidate_names_preview"] = string.Join(" | ", result.Candidates.Take(5).Select(candidate => candidate.Name ?? string.Empty)),
        };

        return diagnostics;
    }

    public ToolResult<RefDescription> WindowsDescribeRef(DescribeRefRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Ref))
        {
            return new ToolResult<RefDescription>(null, new ToolError(
                ToolErrorCode.InvalidArgument,
                "Reference must not be empty.",
                false,
                "Provide an element ref like w1e3.",
                QueryDiagnostics.ForError("windows_describe_ref")));
        }

        var windowRef = TryExtractWindowRef(request.Ref);
        if (windowRef is null)
        {
            return new ToolResult<RefDescription>(null, new ToolError(
                ToolErrorCode.RefNotFound,
                $"Unable to infer window from ref '{request.Ref}'.",
                true,
                "Take a fresh snapshot and use one of the returned refs.",
                QueryDiagnostics.ForError(
                    "windows_describe_ref",
                    null,
                    new Dictionary<string, string?>
                    {
                        ["target_ref"] = request.Ref,
                    },
                    ToolRecoverySemantics.For(ToolErrorCode.RefNotFound))));
        }

        var snapshot = ResolveDescribeSnapshot(windowRef, request.Ref);
        if (snapshot is null)
        {
            var snapshotResult = WindowsSnapshot(new WindowsSnapshotRequest(windowRef));
            if (!snapshotResult.IsSuccess)
            {
                return new ToolResult<RefDescription>(null, snapshotResult.Error);
            }

            snapshot = snapshotResult.Payload!;
        }

        RememberSnapshot(snapshot);
        var description = _locator.Describe(snapshot.Root, windowRef, request.Ref);
        if (description is null)
        {
            return new ToolResult<RefDescription>(null, new ToolError(
                ToolErrorCode.RefNotFound,
                $"Ref '{request.Ref}' was not found in the current snapshot.",
                true,
                "Take a fresh snapshot and retry with a current ref.",
                QueryDiagnostics.Merge(
                    QueryDiagnostics.ForSnapshot(
                        "windows_describe_ref",
                        snapshot,
                        GetSnapshotCacheName(snapshot),
                        _recentLocatorSelector,
                        _recentLocatorRef),
                    ToolRecoverySemantics.For(ToolErrorCode.RefNotFound))));
        }

        var normalizedDescription = description with
        {
            Diagnostics = QueryDiagnostics.ForDescribeRef(
                snapshot,
                description,
                GetSnapshotCacheName(snapshot),
                _recentLocatorSelector,
                _recentLocatorRef),
        };

        return new ToolResult<RefDescription>(normalizedDescription, null);
    }

    public ToolResult<FocusContextResult> WindowsRefreshFocus(WindowsRefreshFocusRequest request)
    {
        try
        {
            var windowRef = request.WindowRef ?? _windowRegistry.GetActiveWindow()?.Ref;
            if (windowRef is null)
            {
                return new ToolResult<FocusContextResult>(null, new ToolError(
                ToolErrorCode.NoActiveWindow,
                "No active window was detected.",
                true,
                "Activate a target window and retry.",
                QueryDiagnostics.ForError("windows_refresh_focus", null, ToolRecoverySemantics.For(ToolErrorCode.NoActiveWindow))));
            }

            var snapshot = RefreshFocusSnapshot(windowRef);
            var path = FindPathToFocused(snapshot.Root);
            for (var attempt = 0; path is null && attempt < 4; attempt++)
            {
                Thread.Sleep(100);
                snapshot = RefreshFocusSnapshot(windowRef);
                path = FindPathToFocused(snapshot.Root);
            }

            if (path is null)
            {
                return new ToolResult<FocusContextResult>(null, new ToolError(
                ToolErrorCode.TargetNotFound,
                "No focused element was found in the current snapshot.",
                true,
                "Refresh the window snapshot and retry after moving focus.",
                QueryDiagnostics.ForError(
                    "windows_refresh_focus",
                    windowRef.Value,
                    new Dictionary<string, string?>
                    {
                        ["snapshot_version"] = snapshot.SnapshotVersion,
                        ["snapshot_cache"] = "focus_context_snapshot",
                    },
                    ToolRecoverySemantics.For(ToolErrorCode.TargetNotFound))));
            }

            var focusNode = path[^1];
            var parentChain = path
                .Take(Math.Max(0, path.Count - 1))
                .Select(node => node.Ref)
                .ToList();
            var parentNode = path.Count > 1 ? path[^2] : null;
            var siblingRefs = parentNode?.Children
                .Where(child => child.Ref != focusNode.Ref)
                .Select(child => child.Ref)
                .Take(GetContextDepth(request.Depth))
                .ToList() ?? [];
            var childRefs = focusNode.Children
                .Select(child => child.Ref)
                .Take(GetContextDepth(request.Depth))
                .ToList();

            var diagnostics = QueryDiagnostics.ForSnapshot(
                "windows_refresh_focus",
                snapshot,
                "focus_context_snapshot",
                _recentLocatorSelector,
                _recentLocatorRef,
                new Dictionary<string, string?>
                {
                    ["focus_depth"] = GetContextDepth(request.Depth).ToString(),
                    ["focus_role"] = focusNode.Role,
                    ["focus_name"] = focusNode.Name,
                    ["parent_count"] = parentChain.Count.ToString(),
                    ["sibling_count"] = siblingRefs.Count.ToString(),
                    ["child_count"] = childRefs.Count.ToString(),
                });

            return new ToolResult<FocusContextResult>(
                new FocusContextResult(
                    windowRef,
                    focusNode.Ref,
                    parentChain,
                    siblingRefs,
                    childRefs,
                    BuildFocusSummary(windowRef, focusNode, parentChain, siblingRefs, childRefs),
                    diagnostics),
                null);
        }
        catch (InvalidOperationException ex)
        {
            return new ToolResult<FocusContextResult>(null, new ToolError(
                ToolErrorCode.WindowNotFound,
                ex.Message,
                false,
                "Refresh the window list and retry with a current window ref.",
                QueryDiagnostics.ForError("windows_refresh_focus", null, ToolRecoverySemantics.For(ToolErrorCode.WindowNotFound))));
        }
    }

    private static IReadOnlyList<ElementNode> Flatten(ElementNode root)
    {
        var nodes = new List<ElementNode> { root };
        foreach (var child in root.Children)
        {
            nodes.AddRange(Flatten(child));
        }

        return nodes;
    }

    private static OpenClaw.Core.Refs.ElementRef? FindFocusedRef(ElementNode root)
    {
        if (root.States.Contains("focused", StringComparer.OrdinalIgnoreCase))
        {
            return root.Ref;
        }

        foreach (var child in root.Children)
        {
            var match = FindFocusedRef(child);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private ToolResult<OpenClaw.Core.Snapshots.SnapshotResult> ResolveScopedSnapshot(SelectorScope scope)
    {
        if (scope.Kind == SelectorScopeKind.ActiveWindow)
        {
            return WindowsSnapshot(new WindowsSnapshotRequest(null));
        }

        var window = _windowRegistry.ListWindows().FirstOrDefault(candidate =>
            candidate.Title.Contains(scope.WindowName ?? string.Empty, StringComparison.OrdinalIgnoreCase));

        if (window is null)
        {
            return new ToolResult<OpenClaw.Core.Snapshots.SnapshotResult>(null, new ToolError(
                ToolErrorCode.WindowNotFound,
                $"No window matched '{scope.WindowName}'.",
                true,
                "Use windows_list to inspect current top-level windows.",
                QueryDiagnostics.ForError(
                    "windows_snapshot",
                    null,
                    new Dictionary<string, string?>
                    {
                        ["scope_name"] = scope.WindowName,
                    },
                    ToolRecoverySemantics.For(ToolErrorCode.WindowNotFound))));
        }

        return WindowsSnapshot(new WindowsSnapshotRequest(window.Ref));
    }

    private static OpenClaw.Core.Refs.WindowRef? TryExtractWindowRef(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference) || reference[0] != 'w')
        {
            return null;
        }

        var index = reference.IndexOf('e');
        if (index <= 1 || index == reference.Length - 1)
        {
            return null;
        }

        var windowPart = reference[..index];
        var elementPart = reference[(index + 1)..];
        if (!windowPart[1..].All(char.IsDigit) || !elementPart.All(char.IsDigit))
        {
            return null;
        }

        return new OpenClaw.Core.Refs.WindowRef(windowPart);
    }

    private OpenClaw.Core.Snapshots.SnapshotResult GetActiveSnapshot()
    {
        var activeWindow = _windowRegistry.GetActiveWindow()
            ?? throw new InvalidOperationException("No active window was detected.");

        return GetSnapshot(activeWindow.Ref, preferActiveCache: true);
    }

    private OpenClaw.Core.Snapshots.SnapshotResult GetSnapshot(OpenClaw.Core.Refs.WindowRef windowRef, bool preferActiveCache)
    {
        if (preferActiveCache && IsCachedActiveSnapshot(windowRef, _activeWindowSnapshot))
        {
            return _activeWindowSnapshot!;
        }

        if (preferActiveCache && IsCachedActiveSnapshot(windowRef, _focusContextSnapshot))
        {
            _activeWindowSnapshot = _focusContextSnapshot;
            return _focusContextSnapshot!;
        }

        var snapshot = _snapshotBuilder.BuildWindowSnapshot(windowRef);
        RememberSnapshot(snapshot);
        if (preferActiveCache)
        {
            _activeWindowSnapshot = snapshot;
        }

        return snapshot;
    }

    private OpenClaw.Core.Snapshots.SnapshotResult RefreshFocusSnapshot(OpenClaw.Core.Refs.WindowRef windowRef)
    {
        var snapshot = _snapshotBuilder.BuildWindowSnapshot(windowRef);
        _focusContextSnapshot = snapshot;
        RememberSnapshot(snapshot);

        if (_windowRegistry.GetActiveWindow()?.Ref == windowRef)
        {
            _activeWindowSnapshot = snapshot;
        }

        return snapshot;
    }

    private void RememberLocate(string selector, string? elementRef)
    {
        _recentLocatorSelector = selector;
        _recentLocatorRef = elementRef;
    }

    private void RememberSnapshot(OpenClaw.Core.Snapshots.SnapshotResult snapshot)
    {
        var node = _recentWindowSnapshots.First;
        while (node is not null)
        {
            var next = node.Next;
            if (node.Value.WindowRef == snapshot.WindowRef)
            {
                _recentWindowSnapshots.Remove(node);
            }

            node = next;
        }

        _recentWindowSnapshots.AddFirst(snapshot);
        while (_recentWindowSnapshots.Count > RecentSnapshotLimit)
        {
            _recentWindowSnapshots.RemoveLast();
        }
    }

    private OpenClaw.Core.Snapshots.SnapshotResult? ResolveDescribeSnapshot(OpenClaw.Core.Refs.WindowRef windowRef, string reference)
    {
        foreach (var snapshot in _recentWindowSnapshots)
        {
            if (SnapshotContainsRef(snapshot, windowRef, reference))
            {
                return snapshot;
            }
        }

        if (SnapshotContainsRef(_focusContextSnapshot, windowRef, reference))
        {
            return _focusContextSnapshot;
        }

        if (SnapshotContainsRef(_activeWindowSnapshot, windowRef, reference))
        {
            return _activeWindowSnapshot;
        }

        return null;
    }

    private static bool SnapshotContainsRef(
        OpenClaw.Core.Snapshots.SnapshotResult? snapshot,
        OpenClaw.Core.Refs.WindowRef windowRef,
        string reference)
    {
        return snapshot is not null
            && snapshot.WindowRef == windowRef
            && ContainsRef(snapshot.Root, reference);
    }

    private static bool ContainsRef(ElementNode node, string reference)
    {
        if (string.Equals(node.Ref.Value, reference, StringComparison.Ordinal))
        {
            return true;
        }

        return node.Children.Any(child => ContainsRef(child, reference));
    }

    private string GetSnapshotCacheName(OpenClaw.Core.Snapshots.SnapshotResult snapshot)
    {
        if (_recentWindowSnapshots.Any(candidate => ReferenceEquals(candidate, snapshot)))
        {
            return "recent_window_snapshot";
        }

        if (ReferenceEquals(snapshot, _focusContextSnapshot))
        {
            return "focus_context_snapshot";
        }

        if (ReferenceEquals(snapshot, _activeWindowSnapshot))
        {
            return "active_window_snapshot";
        }

        return "fresh";
    }

    private static bool IsCachedActiveSnapshot(OpenClaw.Core.Refs.WindowRef windowRef, OpenClaw.Core.Snapshots.SnapshotResult? snapshot)
    {
        return snapshot is not null && snapshot.WindowRef == windowRef;
    }

    private static int GetContextDepth(int depth) => Math.Clamp(depth, 1, 5);

    private static List<ElementNode>? FindPathToFocused(ElementNode root)
    {
        var path = new List<ElementNode>();
        return TryFindFocusedPath(root, path) ? path : null;
    }

    private static bool TryFindFocusedPath(ElementNode node, List<ElementNode> path)
    {
        path.Add(node);
        if (node.States.Contains("focused", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var child in node.Children)
        {
            if (TryFindFocusedPath(child, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static string BuildFocusSummary(
        OpenClaw.Core.Refs.WindowRef windowRef,
        ElementNode focusNode,
        IReadOnlyList<OpenClaw.Core.Refs.ElementRef> parentChain,
        IReadOnlyList<OpenClaw.Core.Refs.ElementRef> siblingRefs,
        IReadOnlyList<OpenClaw.Core.Refs.ElementRef> childRefs)
    {
        return $"Window: {windowRef.Value}\nFocused: {focusNode.Role} \"{focusNode.Name ?? string.Empty}\" [ref={focusNode.Ref.Value}]\nParents: {string.Join(", ", parentChain.Select(parent => parent.Value))}\nSiblings: {string.Join(", ", siblingRefs.Select(sibling => sibling.Value))}\nChildren: {string.Join(", ", childRefs.Select(child => child.Value))}";
    }

}
