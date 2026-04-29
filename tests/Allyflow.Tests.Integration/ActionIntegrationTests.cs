using Allyflow.Core.Actions;
using Allyflow.Core.Errors;
using Allyflow.Protocol.Actions;
using Allyflow.Protocol.Queries;
using Allyflow.Tests.Integration;

namespace Allyflow.Tests.Integration;

[Collection("UIA integration")]
public sealed class ActionIntegrationTests
{
    [Fact]
    public void SetValue_CanUpdateRealFixtureTextbox_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]"));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var result = actionService.WindowsAct(new ActRequest(
            "req-real-set-value",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "10.0.0.1:8080",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(result.IsSuccess, result.Error?.Message ?? result.Payload?.Error?.Message ?? "windows_act failed");
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_act", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("set_value", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Payload.ExecutionPath);
        Assert.True(result.Payload.Verification.Passed);
        Assert.Equal("10.0.0.1:8080", fixture.GetProxyAddressValue());
    }

    [Fact]
    public void Invoke_CanTriggerRealFixtureSaveButton_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]"));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var result = actionService.WindowsAct(new ActRequest(
            "req-real-invoke",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
            5000));

        Assert.True(result.IsSuccess, result.Error?.Message ?? result.Payload?.Error?.Message ?? "windows_act failed");
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_act", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Payload.ExecutionPath);
        Assert.Equal("已保存:127.0.0.1:7890", fixture.GetStatusValue());
    }

    [Fact]
    public void Scenario_I001_CanFillSettingsAndSave_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);
        Assert.Equal(fixture.WindowTitle, listedWindow!.Title);

        var inputLocate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]"));
        Assert.True(inputLocate.IsSuccess);
        Assert.NotNull(inputLocate.Payload);
        Assert.NotNull(inputLocate.Payload!.BestMatch);
        Assert.Equal("windows_locate", inputLocate.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", inputLocate.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", inputLocate.Payload.Diagnostics["primary_interface"]);
        Assert.Equal($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]", inputLocate.Payload.Diagnostics["selector_used"]);

        var setValue = actionService.WindowsAct(new ActRequest(
            "req-scenario-set-value",
            new ActionTarget(inputLocate.Payload.BestMatch.Ref.Value, null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "10.0.0.1:8080",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(setValue.IsSuccess, setValue.Error?.Message ?? setValue.Payload?.Error?.Message ?? "set_value failed");
        Assert.NotNull(setValue.Payload);
        Assert.Equal("windows_act", setValue.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", setValue.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", setValue.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("set_value", setValue.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", setValue.Payload.ExecutionPath);
        Assert.True(setValue.Payload.Verification.Passed);
        Assert.Equal("10.0.0.1:8080", setValue.Payload.ObservedEffect["text_updated"]);

        var buttonLocate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]"));
        Assert.True(buttonLocate.IsSuccess);
        Assert.NotNull(buttonLocate.Payload);
        Assert.NotNull(buttonLocate.Payload!.BestMatch);
        Assert.Equal("windows_locate", buttonLocate.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", buttonLocate.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", buttonLocate.Payload.Diagnostics["primary_interface"]);
        Assert.Equal($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]", buttonLocate.Payload.Diagnostics["selector_used"]);

        var invoke = actionService.WindowsAct(new ActRequest(
            "req-scenario-invoke-save",
            new ActionTarget(buttonLocate.Payload.BestMatch.Ref.Value, null),
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
            5000));

        Assert.True(invoke.IsSuccess, invoke.Error?.Message ?? invoke.Payload?.Error?.Message ?? "invoke failed");
        Assert.NotNull(invoke.Payload);
        Assert.Equal("windows_act", invoke.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", invoke.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", invoke.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", invoke.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", invoke.Payload.ExecutionPath);
        Assert.True(invoke.Payload.Verification.Passed);
        Assert.Equal("no_verification", invoke.Payload.Verification.Type);

        Assert.Equal("10.0.0.1:8080", fixture.GetProxyAddressValue());
        Assert.Equal("已保存:10.0.0.1:8080", fixture.GetStatusValue());
    }

    [Fact]
    public void Toggle_CanUpdateRealFixtureCheckbox_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") checkbox[automation_id=\"AutoDetectCheckbox\"]"));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var result = actionService.WindowsAct(new ActRequest(
            "req-real-toggle",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "toggle",
            new Dictionary<string, object?>
            {
                ["target_state"] = true,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome(
                "property_change",
                new Dictionary<string, object?>
                {
                    ["property"] = "toggle_state",
                    ["value"] = "true",
                },
                1000),
            5000));

        Assert.True(result.IsSuccess, result.Error?.Message ?? result.Payload?.Error?.Message ?? "windows_act failed");
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_act", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("toggle", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Payload.ExecutionPath);
        Assert.True(result.Payload.Verification.Passed);
        Assert.Equal("true", result.Payload.ObservedEffect["toggle_state"]);
        Assert.True(fixture.GetAutoDetectEnabled());
    }

    [Fact]
    public void Focus_CanRefreshFocusedContext_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var selector = $"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"UsernameInput\"]";
        var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var result = actionService.WindowsAct(new ActRequest(
            "req-real-focus",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "focus",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = true,
            },
            new ExpectedOutcome("focus_change", new Dictionary<string, object?>(), 500),
            5000));

        Assert.True(result.IsSuccess, result.Error?.Message ?? result.Payload?.Error?.Message ?? "windows_act failed");
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_act", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("focus", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Payload.ExecutionPath);
        Assert.True(result.Payload.Verification.Passed);
        Assert.Equal("true", result.Payload.ObservedEffect["focus_changed"]);
        Assert.Equal("focus", result.Payload.Diagnostics["action_strategy"]);

        var focus = queryService.WindowsRefreshFocus(new WindowsRefreshFocusRequest(listedWindow!.Ref, 2));

        Assert.True(focus.IsSuccess, focus.Error?.Message ?? "windows_refresh_focus failed");
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

    [Fact]
    public void ExpandCollapse_CanToggleRealFixtureComboBox_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var selector = $"scope:window(name=\"{fixture.WindowTitle}\") combobox[automation_id=\"AdvancedOptionsCombo\"]";
        var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var expand = actionService.WindowsAct(new ActRequest(
            "req-real-expand",
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

        Assert.True(expand.IsSuccess, expand.Error?.Message ?? expand.Payload?.Error?.Message ?? "expand failed");
        Assert.NotNull(expand.Payload);
        Assert.Equal("windows_act", expand.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", expand.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", expand.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("expand", expand.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", expand.Payload.ExecutionPath);
        Assert.Equal("expand_pattern", expand.Payload.Diagnostics["action_strategy"]);
        Assert.True(expand.Payload.Verification.Passed);
        Assert.Equal("Expanded", expand.Payload.ObservedEffect["expand_state"]);
        Assert.True(fixture.GetOptionsExpanded());

        var collapse = actionService.WindowsAct(new ActRequest(
            "req-real-collapse",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "collapse",
            new Dictionary<string, object?>
            {
                ["target_state"] = "Collapsed",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = true,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "expand_state",
                ["value"] = "Collapsed",
            }, 500),
            5000));

        Assert.True(collapse.IsSuccess, collapse.Error?.Message ?? collapse.Payload?.Error?.Message ?? "collapse failed");
        Assert.NotNull(collapse.Payload);
        Assert.Equal("windows_act", collapse.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", collapse.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", collapse.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("collapse", collapse.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", collapse.Payload.ExecutionPath);
        Assert.Equal("collapse_pattern", collapse.Payload.Diagnostics["action_strategy"]);
        Assert.True(collapse.Payload.Verification.Passed);
        Assert.Equal("Collapsed", collapse.Payload.ObservedEffect["expand_state"]);
        Assert.False(fixture.GetOptionsExpanded());
    }

    [Fact]
    public void RangeValue_ReturnsActionNotSupported_OnFixtureSpinnerSurface()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest(
            $"scope:window(name=\"{fixture.WindowTitle}\") spinner[automation_id=\"VolumeSlider\"]"));
        Assert.True(locate.IsSuccess, locate.Error?.Message ?? "windows_locate failed");
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var result = actionService.WindowsAct(new ActRequest(
            "req-real-range-value",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "range_value",
            new Dictionary<string, object?>
            {
                ["target_value"] = 55,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "range_value",
                ["value"] = "55",
            }, 1000),
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Payload);
        Assert.Equal(ToolErrorCode.ActionNotSupported, result.Error!.Code);
        Assert.Equal(ToolErrorCode.ActionNotSupported, result.Payload!.Error!.Code);
        Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("range_value", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("Target does not support RangeValuePattern.", result.Error.Message);
        Assert.Equal(40, fixture.GetVolumeValue());
    }

    [Fact]
    public void Select_CanUpdateRealFixtureComboBox_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest(
            $"scope:window(name=\"{fixture.WindowTitle}\") combobox[automation_id=\"AdvancedOptionsCombo\"]"));
        Assert.True(locate.IsSuccess, locate.Error?.Message ?? "windows_locate failed");
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var result = actionService.WindowsAct(new ActRequest(
            "req-real-select",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "select",
            new Dictionary<string, object?>
            {
                ["option_text"] = "高级模式",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "selected_item",
                ["value"] = "高级模式",
            }, 1000),
            5000));

        Assert.True(result.IsSuccess, result.Error?.Message ?? result.Payload?.Error?.Message ?? "windows_act failed");
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_act", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("select", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Payload.ExecutionPath);
        Assert.Equal("selection_item_pattern", result.Payload.Diagnostics["action_strategy"]);
        Assert.True(result.Payload.Verification.Passed);
        Assert.Equal("高级模式", result.Payload.ObservedEffect["selected_item"]);
        Assert.Equal("高级模式", fixture.GetSelectedOptionValue());
    }

    [Fact]
    public void Scenario_I003_CanReportAmbiguityAndResolveWithRefinedSelector()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var ambiguousLocate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") button[name=\"保存\"]"));
        Assert.False(ambiguousLocate.IsSuccess);
        Assert.NotNull(ambiguousLocate.Payload);
        Assert.Equal(Allyflow.Core.Locators.LocateStatus.Ambiguous, ambiguousLocate.Payload!.Status);
        Assert.Equal(Allyflow.Core.Errors.ToolErrorCode.TargetAmbiguous, ambiguousLocate.Error!.Code);
        Assert.Equal("windows_locate", ambiguousLocate.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", ambiguousLocate.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", ambiguousLocate.Payload.Diagnostics["primary_interface"]);
        Assert.Equal($"scope:window(name=\"{fixture.WindowTitle}\") button[name=\"保存\"]", ambiguousLocate.Payload.Diagnostics["selector_used"]);
        Assert.Equal("2", ambiguousLocate.Payload.Diagnostics["candidate_count"]);

        var refinedLocate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]"));
        Assert.True(refinedLocate.IsSuccess);
        Assert.NotNull(refinedLocate.Payload);
        Assert.NotNull(refinedLocate.Payload!.BestMatch);
        Assert.Equal("windows_locate", refinedLocate.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", refinedLocate.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", refinedLocate.Payload.Diagnostics["primary_interface"]);
        Assert.Equal($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"SaveButton\"]", refinedLocate.Payload.Diagnostics["selector_used"]);

        var invoke = actionService.WindowsAct(new ActRequest(
            "req-i003-resolved-invoke",
            new ActionTarget(refinedLocate.Payload.BestMatch.Ref.Value, null),
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
            5000));

        Assert.True(invoke.IsSuccess, invoke.Error?.Message ?? invoke.Payload?.Error?.Message ?? "invoke failed");
        Assert.NotNull(invoke.Payload);
        Assert.Equal("windows_act", invoke.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", invoke.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", invoke.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", invoke.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", invoke.Payload.ExecutionPath);
        Assert.Equal("已保存:127.0.0.1:7890", fixture.GetStatusValue());
    }

    [Fact]
    public void Scenario_I002_CanFillLoginFormAndSubmit_ThroughStructuredActionPath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var usernameLocate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"UsernameInput\"]"));
        Assert.True(usernameLocate.IsSuccess);
        Assert.NotNull(usernameLocate.Payload);
        Assert.NotNull(usernameLocate.Payload!.BestMatch);

        var setUsername = actionService.WindowsAct(new ActRequest(
            "req-i002-set-username",
            new ActionTarget(usernameLocate.Payload.BestMatch.Ref.Value, null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "alice",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(setUsername.IsSuccess, setUsername.Error?.Message ?? setUsername.Payload?.Error?.Message ?? "set username failed");
        Assert.NotNull(setUsername.Payload);
        Assert.Equal("windows_act", setUsername.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("set_value", setUsername.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", setUsername.Payload.ExecutionPath);
        Assert.True(setUsername.Payload.Verification.Passed);

        var passwordLocate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"PasswordInput\"]"));
        Assert.True(passwordLocate.IsSuccess);
        Assert.NotNull(passwordLocate.Payload);
        Assert.NotNull(passwordLocate.Payload!.BestMatch);

        var setPassword = actionService.WindowsAct(new ActRequest(
            "req-i002-set-password",
            new ActionTarget(passwordLocate.Payload.BestMatch.Ref.Value, null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "s3cr3t!",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(setPassword.IsSuccess, setPassword.Error?.Message ?? setPassword.Payload?.Error?.Message ?? "set password failed");
        Assert.NotNull(setPassword.Payload);
        Assert.Equal("windows_act", setPassword.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("set_value", setPassword.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", setPassword.Payload.ExecutionPath);
        Assert.True(setPassword.Payload.Verification.Passed);

        var loginLocate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"LoginButton\"]"));
        Assert.True(loginLocate.IsSuccess);
        Assert.NotNull(loginLocate.Payload);
        Assert.NotNull(loginLocate.Payload!.BestMatch);

        var submit = actionService.WindowsAct(new ActRequest(
            "req-i002-login",
            new ActionTarget(loginLocate.Payload.BestMatch.Ref.Value, null),
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
            5000));

        Assert.True(submit.IsSuccess, submit.Error?.Message ?? submit.Payload?.Error?.Message ?? "login invoke failed");
        Assert.NotNull(submit.Payload);
        Assert.Equal("windows_act", submit.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", submit.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", submit.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", submit.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", submit.Payload.ExecutionPath);

        Assert.Equal("alice", fixture.GetUsernameValue());
        Assert.Equal("s3cr3t!", fixture.GetPasswordValue());
        Assert.Equal("已登录:alice/s3cr3t!", fixture.GetStatusValue());
    }

    [Fact]
    public void DryRun_DoesNotExecuteRealSetValue_OnStructuredTarget()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]"));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var originalValue = fixture.GetProxyAddressValue();
        var result = actionService.WindowsAct(new ActRequest(
            "req-real-dry-run-set-value",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "203.0.113.7:9000",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
                DryRun = true,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(result.IsSuccess, result.Error?.Message ?? result.Payload?.Error?.Message ?? "dry_run failed");
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_act", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("set_value", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("dry_run", result.Payload.ExecutionPath);
        Assert.Equal("set_value", result.Payload.Diagnostics["preview_action"]);
        Assert.Equal("execution_only", result.Payload.Diagnostics["preview_kind"]);
        Assert.NotNull(result.Payload.Diagnostics["preview_execution_path"]);
        Assert.True(result.Payload.Verification.Passed);
        Assert.Equal("Execution preview completed without running the action.", result.Payload.Verification.Message);
        Assert.Equal(originalValue, fixture.GetProxyAddressValue());
        Assert.Equal("未保存", fixture.GetStatusValue());
    }

    [Fact]
    public void MissingSelector_ReturnsTargetNotFound_WithStandardErrorDiagnostics()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var missingSelector = $"scope:window(name=\"{fixture.WindowTitle}\") button[automation_id=\"MissingButton\"]";
        var result = actionService.WindowsAct(new ActRequest(
            "req-real-missing-target",
            new ActionTarget(null, missingSelector),
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Payload);
        Assert.Equal(Allyflow.Core.Errors.ToolErrorCode.TargetNotFound, result.Error!.Code);
        Assert.Equal(Allyflow.Core.Errors.ToolErrorCode.TargetNotFound, result.Payload!.Error!.Code);
        Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", result.Payload.Diagnostics["action_name"]);
        Assert.Equal(missingSelector, result.Payload.Diagnostics["selector_used"]);
        Assert.Equal("selector", result.Payload.Diagnostics["target_source"]);
        Assert.Equal("0", result.Payload.Diagnostics["retry_count"]);
        Assert.Equal(missingSelector, result.Error.Diagnostics["selector_used"]);
        Assert.Equal("windows_act", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", result.Error.Diagnostics["action_name"]);
        Assert.Equal("selector", result.Error.Diagnostics["target_source"]);
        Assert.Equal("0", result.Error.Diagnostics["retry_count"]);
        Assert.Equal("target_recovery", result.Error.Diagnostics["recovery_kind"]);
        Assert.Equal("windows_snapshot", result.Error.Diagnostics["recovery_target_tool"]);
    }

    [Fact]
    public void VerificationFailure_ReturnsObservedAndExpectedValues_OnRealSetValuePath()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]"));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var result = actionService.WindowsAct(new ActRequest(
            "req-real-verification-failure",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "10.10.10.10:9000",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>
            {
                ["value"] = "mismatch-value",
            }, 1000),
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Payload);
        Assert.Equal(ToolErrorCode.VerificationFailed, result.Error!.Code);
        Assert.Equal(ActionStatus.PartialSuccess, result.Payload!.Status);
        Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("set_value", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Payload.ExecutionPath);
        Assert.False(result.Payload.Verification.Passed);
        Assert.Equal("mismatch-value", result.Payload.Verification.Diagnostics["expected_value"]);
        Assert.Equal("10.10.10.10:9000", result.Payload.Verification.Diagnostics["observed_value"]);
        Assert.Equal("10.10.10.10:9000", fixture.GetProxyAddressValue());
    }

    [Fact]
    public void ClickWithoutFallback_ReturnsFallbackBlocked_OnRealNonInvokeTarget()
    {
        using var fixture = new TestFixtureHost();
        fixture.Start();

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var listedWindow = runtime.WaitForWindow(fixture.WindowTitle);
        Assert.NotNull(listedWindow);

        var locate = queryService.WindowsLocate(new WindowsLocateRequest($"scope:window(name=\"{fixture.WindowTitle}\") edit[automation_id=\"ProxyAddressInput\"]"));
        Assert.True(locate.IsSuccess);
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);

        var originalValue = fixture.GetProxyAddressValue();
        var result = actionService.WindowsAct(new ActRequest(
            "req-real-fallback-blocked",
            new ActionTarget(locate.Payload.BestMatch.Ref.Value, null),
            "click",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
                AllowFallback = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.NotNull(result.Payload);
        Assert.Equal(ToolErrorCode.FallbackBlocked, result.Error!.Code);
        Assert.Equal(ToolErrorCode.FallbackBlocked, result.Payload!.Error!.Code);
        Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("click", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("0", result.Payload.Diagnostics["retry_count"]);
        Assert.Equal("1", result.Payload.Diagnostics["attempt_count"]);
        Assert.Equal("failed", result.Payload.Diagnostics["attempt_1_status"]);
        Assert.Equal("False", result.Payload.Diagnostics["attempt_1_retryable"], ignoreCase: true);
        Assert.Equal(originalValue, fixture.GetProxyAddressValue());
    }

}
