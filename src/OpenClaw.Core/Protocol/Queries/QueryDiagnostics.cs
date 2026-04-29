using OpenClaw.Core.Locators;
using OpenClaw.Core.Refs;
using OpenClaw.Core.Snapshots;

namespace OpenClaw.Protocol.Queries;

internal static class QueryDiagnostics
{
    public static IReadOnlyDictionary<string, string?> ForError(
        string queryKind,
        string? windowRef = null,
        params IReadOnlyDictionary<string, string?>?[] extras)
    {
        return Merge(
            new Dictionary<string, string?>
            {
                ["tool_name"] = queryKind,
                ["query_kind"] = queryKind,
                ["interaction_model"] = "text_structured",
                ["primary_interface"] = "accessibility_tree",
                ["window_ref"] = windowRef,
            },
            Merge(extras));
    }

    public static IReadOnlyDictionary<string, string?> ForWindowsList(
        int windowCount,
        WindowRef? activeWindowRef)
    {
        return Merge(
            new Dictionary<string, string?>
            {
                ["tool_name"] = "windows_list",
                ["query_kind"] = "windows_list",
                ["interaction_model"] = "text_structured",
                ["primary_interface"] = "accessibility_tree",
                ["scope"] = "desktop_top_level_windows",
                ["window_count"] = windowCount.ToString(),
                ["active_window_ref"] = activeWindowRef?.Value,
            });
    }

    public static IReadOnlyDictionary<string, string?> ForSnapshot(
        string queryKind,
        SnapshotResult snapshot,
        string snapshotCache,
        string? recentLocatorSelector,
        string? recentLocatorRef,
        IReadOnlyDictionary<string, string?>? extras = null)
    {
        return Merge(
            snapshot.Diagnostics,
            new Dictionary<string, string?>
            {
                ["tool_name"] = queryKind,
                ["query_kind"] = queryKind,
                ["interaction_model"] = "text_structured",
                ["primary_interface"] = "accessibility_tree",
                ["window_ref"] = snapshot.WindowRef.Value,
                ["snapshot_version"] = snapshot.SnapshotVersion,
                ["backend_used"] = GetValue(snapshot.Diagnostics, "backend_used"),
                ["snapshot_cache"] = snapshotCache,
                ["recent_locator_selector"] = recentLocatorSelector,
                ["recent_locator_ref"] = recentLocatorRef,
            },
            extras);
    }

    public static IReadOnlyDictionary<string, string?> ForLocate(
        SnapshotResult snapshot,
        string selector,
        LocateResult located,
        string? recentLocatorSelector,
        string? recentLocatorRef)
    {
        return Merge(
            located.Diagnostics,
            ForSnapshot("windows_locate", snapshot, "fresh", recentLocatorSelector, recentLocatorRef),
            new Dictionary<string, string?>
            {
                ["selector_used"] = selector,
                ["target_ref"] = located.BestMatch?.Ref.Value,
                ["candidate_count"] = located.Candidates.Count.ToString(),
            });
    }

    public static IReadOnlyDictionary<string, string?> ForDescribeRef(
        SnapshotResult snapshot,
        RefDescription description,
        string snapshotCache,
        string? recentLocatorSelector,
        string? recentLocatorRef)
    {
        return Merge(
            description.Diagnostics,
            ForSnapshot("windows_describe_ref", snapshot, snapshotCache, recentLocatorSelector, recentLocatorRef),
            new Dictionary<string, string?>
            {
                ["target_ref"] = description.Ref.Value,
                ["parent_ref"] = description.ParentRef?.Value,
                ["child_count"] = description.ChildRefs.Count.ToString(),
                ["role"] = description.Role,
            });
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

    private static string? GetValue(IReadOnlyDictionary<string, string?> diagnostics, string key)
    {
        return diagnostics.TryGetValue(key, out var value)
            ? value
            : null;
    }
}
