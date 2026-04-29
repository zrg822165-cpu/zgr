using Allyflow.Core.Actions;
using Allyflow.Protocol.Actions;
using Allyflow.Protocol.Queries;
using Allyflow.Tests.Integration;

namespace Allyflow.Tests.Integration;

[Collection("UIA integration")]
public sealed class QueryIntegrationTests
{
    [Fact]
    public void QueryPipeline_CanListSnapshotLocateAndDescribe_RealFixtureWindow()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var service = runtime.QueryService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var snapshot = service.WindowsSnapshot(new WindowsSnapshotRequest(listedWindow!.Ref));
        Assert.True(snapshot.IsSuccess);
        Assert.NotNull(snapshot.Payload);
        Assert.Equal("windows_snapshot", snapshot.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", snapshot.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", snapshot.Payload.Diagnostics["primary_interface"]);

        var locate = service.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]"));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);
        Assert.Equal("windows_locate", locate.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", locate.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", locate.Payload.Diagnostics["primary_interface"]);
        Assert.Equal($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]", locate.Payload.Diagnostics["selector_used"]);

        var describe = service.WindowsDescribeRef(new DescribeRefRequest(locate.Payload.BestMatch!.Ref.Value));
        Assert.True(describe.IsSuccess);
        Assert.NotNull(describe.Payload);
        Assert.Equal("windows_describe_ref", describe.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", describe.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", describe.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("保存", describe.Payload.Name);
        Assert.Equal("Button", describe.Payload.Role, ignoreCase: true);
    }

    [Fact]
    public void QueryActionRefreshFocus_PreservesRecentLocatorAndFocusedContext()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var selector = $"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]";
        var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var setValue = actionService.WindowsAct(new ActRequest(
            "req-refresh-focus-set-value",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "198.51.100.25:3128",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(setValue.IsSuccess, setValue.Error?.Message ?? setValue.Payload?.Error?.Message ?? "set_value failed");
        Assert.Equal("198.51.100.25:3128", fixture.GetProxyAddressValue());

        var focus = queryService.WindowsRefreshFocus(new WindowsRefreshFocusRequest(listedWindow!.Ref, 2));
        Assert.True(focus.IsSuccess);
        Assert.NotNull(focus.Payload);
        Assert.Equal("windows_refresh_focus", focus.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", focus.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", focus.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("focus_context_snapshot", focus.Payload.Diagnostics["snapshot_cache"]);
        Assert.Equal(selector, focus.Payload.Diagnostics["recent_locator_selector"]);
        Assert.Equal(locate.Payload.BestMatch.Ref.Value, focus.Payload.Diagnostics["recent_locator_ref"]);
        Assert.NotNull(focus.Payload.FocusRef);
        Assert.NotNull(focus.Payload.Diagnostics["focus_role"]);
        Assert.Contains("Focused:", focus.Payload.SummaryText);
        Assert.Contains(focus.Payload.FocusRef.Value, focus.Payload.SummaryText);
    }
}
