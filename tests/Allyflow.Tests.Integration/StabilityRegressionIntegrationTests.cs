using Allyflow.Core.Actions;
using Allyflow.Protocol.Actions;
using Allyflow.Protocol.Queries;
using Allyflow.Tests.Integration;

namespace Allyflow.Tests.Integration;

[Collection("UIA integration")]
public sealed class StabilityRegressionIntegrationTests
{
    private const int RegressionRounds = 3;

    [Fact]
    public void SnapshotLocateActLoop_RemainsStableAcrossRepeatedRealRuns()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        for (var round = 0; round < RegressionRounds; round++)
        {
            var value = $"198.51.100.{20 + round}:31{20 + round}";
            var selector = $"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]";

            var snapshot = queryService.WindowsSnapshot(new WindowsSnapshotRequest(listedWindow!.Ref));
            Assert.True(snapshot.IsSuccess, BuildRoundFailureMessage(round, "snapshot", snapshot.Error?.Message));
            Assert.NotNull(snapshot.Payload);
            Assert.Equal("windows_snapshot", snapshot.Payload!.Diagnostics["tool_name"]);
            Assert.Equal("text_structured", snapshot.Payload.Diagnostics["interaction_model"]);
            Assert.Equal("accessibility_tree", snapshot.Payload.Diagnostics["primary_interface"]);
            Assert.NotNull(snapshot.Payload.Diagnostics["snapshot_version"]);

            var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
            Assert.True(locate.IsSuccess, BuildRoundFailureMessage(round, "locate", locate.Error?.Message));
            Assert.NotNull(locate.Payload);
            Assert.NotNull(locate.Payload!.BestMatch);
            Assert.Equal("windows_locate", locate.Payload.Diagnostics["tool_name"]);
            Assert.Equal(selector, locate.Payload.Diagnostics["selector_used"]);

            var action = actionService.WindowsAct(new ActRequest(
                $"req-stability-set-value-{round}",
                new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
                "set_value",
                new Dictionary<string, object?>
                {
                    ["text"] = value,
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
                5000));

            Assert.True(action.IsSuccess, BuildRoundFailureMessage(round, "set_value", action.Error?.Message ?? action.Payload?.Error?.Message));
            Assert.NotNull(action.Payload);
            AssertStandardActionResult(action.Payload!, "set_value", round);
            Assert.True(action.Payload.Verification.Passed, BuildRoundFailureMessage(round, "set_value verification", action.Payload.Verification.Message));
            Assert.Equal(value, action.Payload.ObservedEffect["text_updated"]);

            Assert.Equal(value, fixture.GetProxyAddressValue());
        }
    }

    [Fact]
    public void FocusRefreshLoop_RemainsStableAcrossRepeatedRealRuns()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        for (var round = 0; round < RegressionRounds; round++)
        {
            var automationId = round % 2 == 0 ? "UsernameInput" : "PasswordInput";
            var selector = $"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"{automationId}\"]";

            var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
            Assert.True(locate.IsSuccess, BuildRoundFailureMessage(round, "locate", locate.Error?.Message));
            Assert.NotNull(locate.Payload);
            Assert.NotNull(locate.Payload!.BestMatch);

            var focusAction = actionService.WindowsAct(new ActRequest(
                $"req-stability-focus-{round}",
                new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
                "focus",
                new Dictionary<string, object?>(),
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = true,
                },
                new ExpectedOutcome("focus_change", new Dictionary<string, object?>(), 500),
                5000));

            Assert.True(focusAction.IsSuccess, BuildRoundFailureMessage(round, "focus", focusAction.Error?.Message ?? focusAction.Payload?.Error?.Message));
            Assert.NotNull(focusAction.Payload);
            AssertStandardActionResult(focusAction.Payload!, "focus", round);
            Assert.Equal("focus", focusAction.Payload.Diagnostics["action_strategy"]);
            Assert.Equal("true", focusAction.Payload.ObservedEffect["focus_changed"]);

            var focus = queryService.WindowsRefreshFocus(new WindowsRefreshFocusRequest(listedWindow!.Ref, 2));
            Assert.True(focus.IsSuccess, BuildRoundFailureMessage(round, "refresh_focus", focus.Error?.Message));
            Assert.NotNull(focus.Payload);
            Assert.NotNull(focus.Payload!.FocusRef);
            Assert.Equal("windows_refresh_focus", focus.Payload.Diagnostics["tool_name"]);
            Assert.Equal("focus_context_snapshot", focus.Payload.Diagnostics["snapshot_cache"]);
            Assert.Equal(selector, focus.Payload.Diagnostics["recent_locator_selector"]);
            Assert.Equal(locate.Payload.BestMatch.Ref.Value, focus.Payload.Diagnostics["recent_locator_ref"]);
            Assert.NotNull(focus.Payload.Diagnostics["focus_role"]);
            Assert.Contains("Focused:", focus.Payload.SummaryText);
            Assert.Contains(focus.Payload.FocusRef.Value, focus.Payload.SummaryText);
        }
    }

    [Fact]
    public void InvokeLoop_RemainsStableAcrossRepeatedRealRuns()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var inputSelector = $"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]";
        var buttonSelector = $"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]";

        for (var round = 0; round < RegressionRounds; round++)
        {
            var value = $"203.0.113.{30 + round}:82{30 + round}";

            var inputLocate = queryService.WindowsLocate(new WindowsLocateRequest(inputSelector));
            Assert.True(inputLocate.IsSuccess, BuildRoundFailureMessage(round, "input locate", inputLocate.Error?.Message));
            Assert.NotNull(inputLocate.Payload);
            Assert.NotNull(inputLocate.Payload!.BestMatch);

            var setValue = actionService.WindowsAct(new ActRequest(
                $"req-stability-invoke-set-value-{round}",
                new ActionTarget(inputLocate.Payload.BestMatch.Ref.Value, null),
                "set_value",
                new Dictionary<string, object?>
                {
                    ["text"] = value,
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
                5000));

            Assert.True(setValue.IsSuccess, BuildRoundFailureMessage(round, "set_value", setValue.Error?.Message ?? setValue.Payload?.Error?.Message));
            Assert.NotNull(setValue.Payload);
            AssertStandardActionResult(setValue.Payload!, "set_value", round);
            Assert.Equal(value, setValue.Payload!.ObservedEffect["text_updated"]);
            Assert.Equal(value, fixture.GetProxyAddressValue());

            var buttonLocate = queryService.WindowsLocate(new WindowsLocateRequest(buttonSelector));
            Assert.True(buttonLocate.IsSuccess, BuildRoundFailureMessage(round, "button locate", buttonLocate.Error?.Message));
            Assert.NotNull(buttonLocate.Payload);
            Assert.NotNull(buttonLocate.Payload!.BestMatch);
            Assert.Equal("windows_locate", buttonLocate.Payload.Diagnostics["tool_name"]);
            Assert.Equal(buttonSelector, buttonLocate.Payload.Diagnostics["selector_used"]);

            var invoke = actionService.WindowsAct(new ActRequest(
                $"req-stability-invoke-{round}",
                new ActionTarget(buttonLocate.Payload.BestMatch.Ref.Value, null),
                "invoke",
                new Dictionary<string, object?>(),
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
                5000));

            Assert.True(invoke.IsSuccess, BuildRoundFailureMessage(round, "invoke", invoke.Error?.Message ?? invoke.Payload?.Error?.Message));
            Assert.NotNull(invoke.Payload);
            AssertStandardActionResult(invoke.Payload!, "invoke", round);

            Assert.Equal($"已保存:{value}", fixture.GetStatusValue());
        }
    }

    [Fact]
    public void ToggleLoop_RemainsStableAcrossRepeatedRealRuns()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var selector = $"scope:window(name=\"{fixture.WindowTitle}\") checkbox[automation_id=\"AutoDetectCheckbox\"]";

        for (var round = 0; round < RegressionRounds; round++)
        {
            var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
            Assert.True(locate.IsSuccess, BuildRoundFailureMessage(round, "locate", locate.Error?.Message));
            Assert.NotNull(locate.Payload);
            Assert.NotNull(locate.Payload!.BestMatch);
            Assert.Equal("windows_locate", locate.Payload.Diagnostics["tool_name"]);
            Assert.Equal(selector, locate.Payload.Diagnostics["selector_used"]);

            var targetState = round % 2 == 0;
            var expectedToggleState = targetState.ToString().ToLowerInvariant();

            var toggle = actionService.WindowsAct(new ActRequest(
                $"req-stability-toggle-{round}",
                new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
                "toggle",
                new Dictionary<string, object?>
                {
                    ["target_state"] = targetState,
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("property_change", new Dictionary<string, object?>
                {
                    ["property"] = "toggle_state",
                    ["value"] = expectedToggleState,
                }, 1000),
                5000));

            Assert.True(toggle.IsSuccess, BuildRoundFailureMessage(round, "toggle", toggle.Error?.Message ?? toggle.Payload?.Error?.Message));
            Assert.NotNull(toggle.Payload);
            AssertStandardActionResult(toggle.Payload!, "toggle", round);
            Assert.Equal("toggle_pattern", toggle.Payload.Diagnostics["action_strategy"]);
            Assert.True(toggle.Payload.Verification.Passed, BuildRoundFailureMessage(round, "toggle verification", toggle.Payload.Verification.Message));
            Assert.Equal(expectedToggleState, toggle.Payload.ObservedEffect["toggle_state"]);

            Assert.Equal(targetState, fixture.GetAutoDetectEnabled());
        }
    }

    [Fact]
    public void ExpandCollapseLoop_RemainsStableAcrossRepeatedRealRuns()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var selector = $"scope:window(name=\"{fixture.WindowTitle}\") combobox[automation_id=\"AdvancedOptionsCombo\"]";

        for (var round = 0; round < RegressionRounds; round++)
        {
            var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
            Assert.True(locate.IsSuccess, BuildRoundFailureMessage(round, "locate", locate.Error?.Message));
            Assert.NotNull(locate.Payload);
            Assert.NotNull(locate.Payload!.BestMatch);
            Assert.Equal("windows_locate", locate.Payload.Diagnostics["tool_name"]);
            Assert.Equal(selector, locate.Payload.Diagnostics["selector_used"]);

            var expand = actionService.WindowsAct(new ActRequest(
                $"req-stability-expand-{round}",
                new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
                "expand",
                new Dictionary<string, object?>
                {
                    ["target_state"] = "Expanded",
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("property_change", new Dictionary<string, object?>
                {
                    ["property"] = "expand_state",
                    ["value"] = "Expanded",
                }, 500),
                5000));

            Assert.True(expand.IsSuccess, BuildRoundFailureMessage(round, "expand", expand.Error?.Message ?? expand.Payload?.Error?.Message));
            Assert.NotNull(expand.Payload);
            AssertStandardActionResult(expand.Payload!, "expand", round);
            Assert.Equal("expand_pattern", expand.Payload.Diagnostics["action_strategy"]);
            Assert.True(expand.Payload.Verification.Passed, BuildRoundFailureMessage(round, "expand verification", expand.Payload.Verification.Message));
            Assert.Equal("Expanded", expand.Payload.ObservedEffect["expand_state"]);
            WaitForCondition(
                () => fixture.GetOptionsExpanded(),
                BuildRoundFailureMessage(round, "expand fixture state", "ComboBox did not report expanded state in time."));
            Assert.True(fixture.GetOptionsExpanded());

            var collapse = actionService.WindowsAct(new ActRequest(
                $"req-stability-collapse-{round}",
                new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
                "collapse",
                new Dictionary<string, object?>
                {
                    ["target_state"] = "Collapsed",
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("property_change", new Dictionary<string, object?>
                {
                    ["property"] = "expand_state",
                    ["value"] = "Collapsed",
                }, 500),
                5000));

            Assert.True(collapse.IsSuccess, BuildRoundFailureMessage(round, "collapse", collapse.Error?.Message ?? collapse.Payload?.Error?.Message));
            Assert.NotNull(collapse.Payload);
            AssertStandardActionResult(collapse.Payload!, "collapse", round);
            Assert.Equal("collapse_pattern", collapse.Payload.Diagnostics["action_strategy"]);
            Assert.True(collapse.Payload.Verification.Passed, BuildRoundFailureMessage(round, "collapse verification", collapse.Payload.Verification.Message));
            Assert.Equal("Collapsed", collapse.Payload.ObservedEffect["expand_state"]);
            WaitForCondition(
                () => !fixture.GetOptionsExpanded(),
                BuildRoundFailureMessage(round, "collapse fixture state", "ComboBox did not report collapsed state in time."));
            Assert.False(fixture.GetOptionsExpanded());
        }
    }

    private static void AssertStandardActionResult(ActionResult action, string actionName, int round)
    {
        Assert.Equal("windows_act", action.Diagnostics["tool_name"]);
        Assert.Equal(actionName, action.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", action.ExecutionPath);
        Assert.Equal(0, action.Retries);
        Assert.True(action.Timing.DurationMs >= 0, BuildRoundFailureMessage(round, $"{actionName} timing", $"DurationMs was {action.Timing.DurationMs}"));
        Assert.True(action.Timing.FinishedAt >= action.Timing.StartedAt, BuildRoundFailureMessage(round, $"{actionName} timing", $"FinishedAt {action.Timing.FinishedAt:o} was earlier than StartedAt {action.Timing.StartedAt:o}"));
    }

    private static string BuildRoundFailureMessage(int round, string step, string? detail)
    {
        return $"Round {round + 1} [{step}] failed: {detail ?? "<no details>"}";
    }

    private static void WaitForCondition(Func<bool> condition, string failureMessage)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(100);
        }

        Assert.True(condition(), failureMessage);
    }
}
