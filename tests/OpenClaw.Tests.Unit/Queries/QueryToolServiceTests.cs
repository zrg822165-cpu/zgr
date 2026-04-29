using OpenClaw.Core.Abstractions;
using OpenClaw.Core.Errors;
using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;
using OpenClaw.Core.Snapshots;
using OpenClaw.Protocol.Queries;

namespace OpenClaw.Tests.Unit.Queries;

public sealed class QueryToolServiceTests
{
    [Fact]
    public void WindowsLocate_ReturnsTargetNotFoundError_WhenSelectorDoesNotMatch()
    {
        var service = new QueryToolService(new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsLocate(new WindowsLocateRequest("button[name=\"不存在\"]"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_locate", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal(ToolErrorCode.TargetNotFound, result.Error!.Code);
        Assert.Equal("windows_locate", result.Payload.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("w1", result.Payload.Diagnostics["window_ref"]);
        Assert.Equal("snap-stub", result.Payload.Diagnostics["snapshot_version"]);
        Assert.Equal("uia", result.Payload.Diagnostics["backend_used"]);
        Assert.Equal("button[name=\"不存在\"]", result.Payload.Diagnostics["selector_used"]);
        Assert.Equal("target_recovery", result.Error.Diagnostics["recovery_kind"]);
        Assert.Equal("windows_snapshot", result.Error.Diagnostics["recovery_target_tool"]);
    }

    [Fact]
    public void WindowsLocate_InvalidSelector_EmitsNormalizedErrorDiagnostics()
    {
        var service = new QueryToolService(new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsLocate(new WindowsLocateRequest("button[name=\"保存\""));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ToolErrorCode.InvalidArgument, result.Error!.Code);
        Assert.Equal("windows_locate", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("windows_locate", result.Error.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
        Assert.Equal("button[name=\"保存\"", result.Error.Diagnostics["selector_used"]);
        Assert.NotNull(result.Error.Diagnostics["error_position"]);
    }

    [Fact]
    public void WindowsList_EmitsNormalizedDiagnostics()
    {
        var service = new QueryToolService(new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsList();

        Assert.Equal("windows_list", result.Diagnostics["tool_name"]);
        Assert.Equal("windows_list", result.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Diagnostics["primary_interface"]);
        Assert.Equal("desktop_top_level_windows", result.Diagnostics["scope"]);
        Assert.Equal("1", result.Diagnostics["window_count"]);
        Assert.Equal("w1", result.Diagnostics["active_window_ref"]);
    }

    [Fact]
    public void WindowsSnapshot_EmitsNormalizedDiagnostics()
    {
        var service = new QueryToolService(new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsSnapshot(new WindowsSnapshotRequest(null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_snapshot", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("windows_snapshot", result.Payload!.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("w1", result.Payload.Diagnostics["window_ref"]);
        Assert.Equal("snap-stub", result.Payload.Diagnostics["snapshot_version"]);
        Assert.Equal("uia", result.Payload.Diagnostics["backend_used"]);
        Assert.Equal("active_window_snapshot", result.Payload.Diagnostics["snapshot_cache"]);
    }

    [Fact]
    public void WindowsRefreshFocus_ReturnsFocusedContextAndRecentLocateDiagnostics()
    {
        var snapshotBuilder = new StubSnapshotBuilder();
        var service = new QueryToolService(new StubWindowRegistry(), snapshotBuilder);

        var locate = service.WindowsLocate(new WindowsLocateRequest("button[name=\"保存\"]"));
        var result = service.WindowsRefreshFocus(new WindowsRefreshFocusRequest(null, 2));

        Assert.True(locate.IsSuccess);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("w1e3", result.Payload!.FocusRef!.Value);
        Assert.Collection(result.Payload.ParentChain, item => Assert.Equal("w1e1", item.Value));
        Assert.Collection(result.Payload.SiblingRefs, item => Assert.Equal("w1e2", item.Value));
        Assert.Collection(result.Payload.ChildRefs, item => Assert.Equal("w1e4", item.Value));
        Assert.Equal("windows_refresh_focus", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("focus_context_snapshot", result.Payload.Diagnostics["snapshot_cache"]);
        Assert.Equal("windows_refresh_focus", result.Payload.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("w1", result.Payload.Diagnostics["window_ref"]);
        Assert.Equal("snap-stub", result.Payload.Diagnostics["snapshot_version"]);
        Assert.Equal("uia", result.Payload.Diagnostics["backend_used"]);
        Assert.Equal("button[name=\"保存\"]", result.Payload.Diagnostics["recent_locator_selector"]);
        Assert.Equal("w1e3", result.Payload.Diagnostics["recent_locator_ref"]);
        Assert.Contains("Focused: button \"保存\" [ref=w1e3]", result.Payload.SummaryText);
    }

    [Fact]
    public void WindowsRefreshFocus_NoActiveWindow_EmitsNormalizedErrorDiagnostics()
    {
        var service = new QueryToolService(new NullWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsRefreshFocus(new WindowsRefreshFocusRequest(null, 2));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ToolErrorCode.NoActiveWindow, result.Error!.Code);
        Assert.Equal("windows_refresh_focus", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("windows_refresh_focus", result.Error.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
        Assert.Equal("surface_recovery", result.Error.Diagnostics["recovery_kind"]);
        Assert.Equal("windows_list", result.Error.Diagnostics["recovery_target_tool"]);
    }

    [Fact]
    public void WindowsActive_ReusesFocusSnapshotCache_WhenAvailable()
    {
        var snapshotBuilder = new StubSnapshotBuilder();
        var service = new QueryToolService(new StubWindowRegistry(), snapshotBuilder);

        var focusResult = service.WindowsRefreshFocus(new WindowsRefreshFocusRequest(null, 2));
        var activeResult = service.WindowsActive();

        Assert.True(focusResult.IsSuccess);
        Assert.True(activeResult.IsSuccess);
        Assert.NotNull(activeResult.Payload);
        Assert.Equal(1, snapshotBuilder.BuildCount);
        Assert.Equal("windows_active", activeResult.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("active_window_snapshot", activeResult.Payload!.Diagnostics["snapshot_cache"]);
        Assert.Equal("windows_active", activeResult.Payload.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", activeResult.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", activeResult.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("w1", activeResult.Payload.Diagnostics["window_ref"]);
        Assert.Equal("snap-stub", activeResult.Payload.Diagnostics["snapshot_version"]);
        Assert.Equal("w1e3", activeResult.Payload.FocusRef!.Value);
    }

    [Fact]
    public void WindowsDescribeRef_ReturnsDescriptionForCurrentSnapshotRef()
    {
        var service = new QueryToolService(new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsDescribeRef(new DescribeRefRequest("w1e3"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("button", result.Payload!.Role);
        Assert.Equal("w1", result.Payload.WindowRef.Value);
        Assert.Equal("windows_describe_ref", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("windows_describe_ref", result.Payload.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("w1e3", result.Payload.Diagnostics["target_ref"]);
        Assert.Equal("button", result.Payload.Diagnostics["role"]);
    }

    [Fact]
    public void WindowsDescribeRef_ReusesRecentSnapshotForLocatedRef()
    {
        var snapshotBuilder = new IncrementingRefSnapshotBuilder();
        var service = new QueryToolService(new StubWindowRegistry(), snapshotBuilder);

        var snapshot = service.WindowsSnapshot(new WindowsSnapshotRequest(new WindowRef("w1")));
        var locate = service.WindowsLocate(new WindowsLocateRequest("button[name=\"保存\"]"));
        var describe = service.WindowsDescribeRef(new DescribeRefRequest("w1e8"));

        Assert.True(snapshot.IsSuccess);
        Assert.True(locate.IsSuccess);
        Assert.True(describe.IsSuccess);
        Assert.NotNull(describe.Payload);
        Assert.Equal("w1e8", locate.Payload!.BestMatch!.Ref.Value);
        Assert.Equal("w1e8", describe.Payload!.Ref.Value);
        Assert.Equal("recent_window_snapshot", describe.Payload.Diagnostics["snapshot_cache"]);
        Assert.Equal(2, snapshotBuilder.BuildCount);
    }

    [Fact]
    public void WindowsDescribeRef_EmptyRef_EmitsNormalizedErrorDiagnostics()
    {
        var service = new QueryToolService(new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsDescribeRef(new DescribeRefRequest(""));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ToolErrorCode.InvalidArgument, result.Error!.Code);
        Assert.Equal("windows_describe_ref", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("windows_describe_ref", result.Error.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
    }

    [Fact]
    public void WindowsDescribeRef_UnparseableRef_EmitsNormalizedErrorDiagnostics()
    {
        var service = new QueryToolService(new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = service.WindowsDescribeRef(new DescribeRefRequest("bad-ref"));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ToolErrorCode.RefNotFound, result.Error!.Code);
        Assert.Equal("windows_describe_ref", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("windows_describe_ref", result.Error.Diagnostics["query_kind"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
        Assert.Equal("bad-ref", result.Error.Diagnostics["target_ref"]);
        Assert.Equal("target_recovery", result.Error.Diagnostics["recovery_kind"]);
        Assert.Equal("refresh_snapshot_then_relocate", result.Error.Diagnostics["suggested_next_step_kind"]);
    }

    private sealed class StubWindowRegistry : IWindowRegistry
    {
        private readonly WindowSummary _window = new(new WindowRef("w1"), "设置", 1234, "WPF", true);

        public WindowSummary? GetActiveWindow() => _window;

        public nint? GetNativeHandle(WindowRef windowRef) => windowRef == _window.Ref ? 1 : null;

        public IReadOnlyList<WindowSummary> ListWindows() => [_window];
    }

    private sealed class NullWindowRegistry : IWindowRegistry
    {
        public WindowSummary? GetActiveWindow() => null;

        public nint? GetNativeHandle(WindowRef windowRef) => null;

        public IReadOnlyList<WindowSummary> ListWindows() => Array.Empty<WindowSummary>();
    }

    private sealed class StubSnapshotBuilder : ISnapshotBuilder
    {
        public int BuildCount { get; private set; }

        public SnapshotResult BuildActiveWindowSnapshot() => BuildWindowSnapshot(new WindowRef("w1"));

        public SnapshotResult BuildWindowSnapshot(WindowRef windowRef)
        {
            BuildCount++;

            var root = new ElementNode(
                new ElementRef("w1e1"),
                "window",
                "设置",
                null,
                null,
                null,
                ["visible"],
                Array.Empty<string>(),
                [
                    new ElementNode(
                        new ElementRef("w1e2"),
                        "text",
                        "说明",
                        null,
                        null,
                        null,
                        ["visible"],
                        Array.Empty<string>(),
                        Array.Empty<ElementNode>()),
                    new ElementNode(
                        new ElementRef("w1e3"),
                        "button",
                        "保存",
                        null,
                        null,
                        null,
                        ["enabled", "visible", "focused"],
                        ["invoke"],
                        [
                            new ElementNode(
                                new ElementRef("w1e4"),
                                "text",
                                "保存当前设置",
                                null,
                                null,
                                null,
                                ["visible"],
                                Array.Empty<string>(),
                                Array.Empty<ElementNode>())
                        ])
                ]);

            return new SnapshotResult(windowRef, "snap-stub", root, "summary", new Dictionary<string, string?>
            {
                ["backend_used"] = "uia",
            });
        }
    }

    private sealed class IncrementingRefSnapshotBuilder : ISnapshotBuilder
    {
        public int BuildCount { get; private set; }

        public SnapshotResult BuildActiveWindowSnapshot() => BuildWindowSnapshot(new WindowRef("w1"));

        public SnapshotResult BuildWindowSnapshot(WindowRef windowRef)
        {
            BuildCount++;
            var buttonRef = BuildCount == 1 ? "w1e3" : "w1e8";

            var root = new ElementNode(
                new ElementRef("w1e1"),
                "window",
                "设置",
                null,
                null,
                null,
                ["visible"],
                Array.Empty<string>(),
                [
                    new ElementNode(
                        new ElementRef(buttonRef),
                        "button",
                        "保存",
                        null,
                        null,
                        null,
                        ["enabled", "visible"],
                        ["invoke"],
                        Array.Empty<ElementNode>())
                ]);

            return new SnapshotResult(windowRef, $"snap-{BuildCount}", root, "summary", new Dictionary<string, string?>
            {
                ["backend_used"] = "uia",
            });
        }
    }
}
