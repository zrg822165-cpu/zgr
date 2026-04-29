using Allyflow.Core.Abstractions;
using Allyflow.Core.Actions;
using Allyflow.Core.Errors;
using Allyflow.Core.Models;
using Allyflow.Core.Refs;
using Allyflow.Protocol.Actions;

namespace Allyflow.Tests.Unit.Actions;

public sealed class ActionToolServiceTests
{
    [Fact]
    public void WindowsAct_DryRun_ReturnsStructuredActionResult()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor());

        var result = service.WindowsAct(new ActRequest(
            "req-dry-run",
            new ActionTarget("w1e2", null),
            "invoke",
            new Dictionary<string, object?>(),
            new ExecutionPolicy(true, 0, false, true, true, true),
            null,
            5000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal(ActionStatus.Success, result.Payload!.Status);
        Assert.Equal("dry_run", result.Payload.ExecutionPath);
        Assert.Equal("ref", result.Payload.TargetResolution!.ResolutionSource);
        Assert.Equal("uia_pattern -> input_simulation", result.Payload.ObservedEffect["preview_execution_path"]);
        Assert.Equal("invoke", result.Payload.Diagnostics["preview_action"]);
        Assert.Equal("execution_only", result.Payload.Diagnostics["preview_kind"]);
        Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", result.Payload.Diagnostics["action_name"]);
    }

    [Fact]
    public void WindowsAct_TargetResolutionFailure_MapsStandardError()
    {
        var error = new ToolError(
            ToolErrorCode.TargetNotFound,
            "No candidate matched the selector.",
            true,
            "Refresh the snapshot or relax the selector.",
            new Dictionary<string, string?>());

        var service = new ActionToolService(
            new StubTargetResolver(success: false, error),
            new StubActionExecutor());

        var result = service.WindowsAct(new ActRequest(
            "req-missing",
            new ActionTarget(null, "button[name=\"保存\"]"),
            "invoke",
            new Dictionary<string, object?>(),
            null,
            null,
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal(ToolErrorCode.TargetNotFound, result.Error!.Code);
        Assert.Equal(ActionStatus.Failed, result.Payload!.Status);
        Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("windows_act", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
        Assert.Equal("invoke", result.Error.Diagnostics["action_name"]);
        Assert.Equal("selector", result.Error.Diagnostics["target_source"]);
        Assert.Equal("button[name=\"保存\"]", result.Error.Diagnostics["selector_used"]);
        Assert.Equal("0", result.Error.Diagnostics["retry_count"]);
        Assert.Equal("target_recovery", result.Error.Diagnostics["recovery_kind"]);
    }

    [Fact]
    public void WindowsAct_ExecutionSuccess_ReturnsVerificationAndDiagnostics()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["text_updated"] = "127.0.0.1:7890",
                },
                new Dictionary<string, string?>
                {
                    ["action_strategy"] = "value_pattern",
                })));

        var result = service.WindowsAct(new ActRequest(
            "req-set",
            new ActionTarget(null, "edit[name=\"代理地址\"]"),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "127.0.0.1:7890",
            },
            null,
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("uia_pattern", result.Payload!.ExecutionPath);
        Assert.True(result.Payload.Verification.Passed);
        Assert.Equal("selector", result.Payload.TargetResolution!.ResolutionSource);
        Assert.Equal("value_pattern", result.Payload.Diagnostics["action_strategy"]);
        Assert.Equal("127.0.0.1:7890", result.Payload.Verification.Diagnostics["expected_value"]);
        Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal("set_value", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Payload.Diagnostics["execution_path"]);
    }

    [Fact]
    public void WindowsAct_FocusSuccess_VerifiesFocusChangeOutcome()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["focus_changed"] = "true",
                },
                new Dictionary<string, string?>
                {
                    ["action_strategy"] = "focus",
                })));

        var result = service.WindowsAct(new ActRequest(
            "req-focus",
            new ActionTarget("w1e2", null),
            "focus",
            new Dictionary<string, object?>(),
            null,
            new ExpectedOutcome("focus_change", new Dictionary<string, object?>(), 1000),
            5000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.True(result.Payload!.Verification.Passed);
        Assert.Equal("focus_change", result.Payload.Verification.Type);
        Assert.Equal("true", result.Payload.ObservedEffect["focus_changed"]);
        Assert.Equal("focus", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("focus", result.Payload.Diagnostics["action_strategy"]);
    }

    [Fact]
    public void WindowsAct_ExpandSuccess_VerifiesExpandStateOutcome()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["expand_state"] = "Expanded",
                },
                new Dictionary<string, string?>
                {
                    ["action_strategy"] = "expand_pattern",
                })));

        var result = service.WindowsAct(new ActRequest(
            "req-expand",
            new ActionTarget("w1e2", null),
            "expand",
            new Dictionary<string, object?>
            {
                ["target_state"] = "Expanded",
            },
            null,
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "expand_state",
                ["value"] = "Expanded",
            }, 1000),
            5000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.True(result.Payload!.Verification.Passed);
        Assert.Equal("property_change", result.Payload.Verification.Type);
        Assert.Equal("Expanded", result.Payload.ObservedEffect["expand_state"]);
        Assert.Equal("expand", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("expand_pattern", result.Payload.Diagnostics["action_strategy"]);
    }

    [Fact]
    public void WindowsAct_RangeValueSuccess_VerifiesRangeValueOutcome()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["range_value"] = "55",
                    ["previous_range_value"] = "40",
                },
                new Dictionary<string, string?>
                {
                    ["action_strategy"] = "range_value_pattern",
                })));

        var result = service.WindowsAct(new ActRequest(
            "req-range",
            new ActionTarget("w1e2", null),
            "range_value",
            new Dictionary<string, object?>
            {
                ["target_value"] = 55,
            },
            null,
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "range_value",
                ["value"] = "55",
            }, 1000),
            5000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.True(result.Payload!.Verification.Passed);
        Assert.Equal("property_change", result.Payload.Verification.Type);
        Assert.Equal("55", result.Payload.ObservedEffect["range_value"]);
        Assert.Equal("40", result.Payload.ObservedEffect["previous_range_value"]);
        Assert.Equal("range_value", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("range_value_pattern", result.Payload.Diagnostics["action_strategy"]);
    }

    [Fact]
    public void WindowsAct_SelectSuccess_VerifiesSelectedItemOutcome()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["selected_item"] = "高级模式",
                    ["previous_selected_item"] = "标准模式",
                },
                new Dictionary<string, string?>
                {
                    ["action_strategy"] = "selection_item_pattern",
                })));

        var result = service.WindowsAct(new ActRequest(
            "req-select",
            new ActionTarget("w1e2", null),
            "select",
            new Dictionary<string, object?>
            {
                ["option_text"] = "高级模式",
            },
            null,
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "selected_item",
                ["value"] = "高级模式",
            }, 1000),
            5000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.True(result.Payload!.Verification.Passed);
        Assert.Equal("property_change", result.Payload.Verification.Type);
        Assert.Equal("高级模式", result.Payload.ObservedEffect["selected_item"]);
        Assert.Equal("标准模式", result.Payload.ObservedEffect["previous_selected_item"]);
        Assert.Equal("select", result.Payload.Diagnostics["action_name"]);
        Assert.Equal("selection_item_pattern", result.Payload.Diagnostics["action_strategy"]);
    }

    [Fact]
    public void WindowsAct_ExecutionSuccessButVerificationFails_ReturnsPartialSuccess()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["toggle_state"] = "false",
                },
                new Dictionary<string, string?>())));

        var result = service.WindowsAct(new ActRequest(
            "req-verify",
            new ActionTarget("w1e2", null),
            "toggle",
            new Dictionary<string, object?>
            {
                ["target_state"] = true,
            },
            null,
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "toggle_state",
                ["value"] = "true",
            }, 1000),
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal(ActionStatus.PartialSuccess, result.Payload!.Status);
        Assert.Equal(ToolErrorCode.VerificationFailed, result.Error!.Code);
        Assert.False(result.Payload.Verification.Passed);
        Assert.Equal("false", result.Payload.Verification.Diagnostics["observed_value"]);
    }

    [Fact]
    public void WindowsAct_RetryableFailuresBeyondBudget_ReturnsFallbackExhausted()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new SequencedActionExecutor(
                ToolErrorCode.TargetStale,
                retryable: true,
                totalFailures: 3));

        var result = service.WindowsAct(new ActRequest(
            "req-retry-budget",
            new ActionTarget("w1e2", null),
            "click",
            new Dictionary<string, object?>(),
            new ExecutionPolicy(true, 2, false, true, true, false),
            null,
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal(ToolErrorCode.FallbackExhausted, result.Error!.Code);
        Assert.Equal(2, result.Payload!.Retries);
        Assert.Equal("3", result.Payload.Diagnostics["attempt_count"]);
        Assert.Equal("TargetStale", result.Payload.Diagnostics["final_error_code"]);
        Assert.Equal("failed", result.Payload.Diagnostics["attempt_3_status"]);
        Assert.Equal("target_recovery", result.Error.Diagnostics["recovery_kind"]);
        Assert.Equal("refresh_snapshot_then_relocate", result.Error.Diagnostics["suggested_next_step_kind"]);
    }

    [Fact]
    public void WindowsAct_NonRetryableExecutionFailure_ReportsAttemptDiagnostics()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new FailingActionExecutor(new ToolError(
                ToolErrorCode.FallbackBlocked,
                "Fallback is blocked by policy.",
                false,
                "Allow fallback or choose a different target.",
                new Dictionary<string, string?>())));

        var result = service.WindowsAct(new ActRequest(
            "req-fallback-blocked",
            new ActionTarget("w1e2", null),
            "click",
            new Dictionary<string, object?>(),
            new ExecutionPolicy(false, 2, false, true, true, false),
            null,
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal(ToolErrorCode.FallbackBlocked, result.Error!.Code);
        Assert.Equal(0, result.Payload!.Retries);
        Assert.Equal("1", result.Payload.Diagnostics["attempt_count"]);
        Assert.Equal("failed", result.Payload.Diagnostics["attempt_1_status"]);
        Assert.Equal("False", result.Payload.Diagnostics["attempt_1_retryable"], ignoreCase: true);
        Assert.Equal("windows_act", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
        Assert.Equal("click", result.Error.Diagnostics["action_name"]);
        Assert.Equal("0", result.Error.Diagnostics["retry_count"]);
        Assert.Equal("1", result.Error.Diagnostics["attempt_count"]);
        Assert.Equal("failed", result.Error.Diagnostics["attempt_1_status"]);
    }

    [Fact]
    public void WindowsAct_VerificationFailure_NormalizesTopLevelErrorDiagnostics()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true),
            new StubActionExecutor(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>
                {
                    ["text_updated"] = "actual-value",
                },
                new Dictionary<string, string?>
                {
                    ["action_strategy"] = "value_pattern",
                })));

        var result = service.WindowsAct(new ActRequest(
            "req-verify-error-diagnostics",
            new ActionTarget("w1e2", null),
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = "expected-value",
            },
            null,
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>
            {
                ["value"] = "different-value",
            }, 1000),
            5000));

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(ToolErrorCode.VerificationFailed, result.Error!.Code);
        Assert.Equal("windows_act", result.Error.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Error.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Error.Diagnostics["primary_interface"]);
        Assert.Equal("set_value", result.Error.Diagnostics["action_name"]);
        Assert.Equal("uia_pattern", result.Error.Diagnostics["execution_path"]);
        Assert.Equal("different-value", result.Error.Diagnostics["expected_value"]);
        Assert.Equal("actual-value", result.Error.Diagnostics["observed_value"]);
        Assert.Equal("0", result.Error.Diagnostics["retry_count"]);
        Assert.Equal("verification_recheck", result.Error.Diagnostics["recovery_kind"]);
    }

    [Fact]
    public void WindowsAct_ResolvedBySelectorRecovery_EmitsRecoveryDiagnostics()
    {
        var service = new ActionToolService(
            new StubTargetResolver(success: true, resolutionFactory: target => new TargetResolution(
                "selector_recovery",
                new ElementRef("w1e5"),
                new WindowRef("w1"),
                target.Selector ?? "scope:active_window button[name=\"保存\"]",
                0.91d,
                new Dictionary<string, string?>
                {
                    ["selector_source"] = "selector_hint",
                    ["recovered_from_stale_ref"] = "true",
                    ["resolved_ref"] = "w1e5",
                })),
            new StubActionExecutor());

        var result = service.WindowsAct(new ActRequest(
            "req-selector-recovery",
            new ActionTarget("w1e2", null),
            "invoke",
            new Dictionary<string, object?>(),
            null,
            null,
            5000));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("selector_recovery", result.Payload!.TargetResolution!.ResolutionSource);
        Assert.Equal("selector_hint", result.Payload.Diagnostics["selector_source"]);
        Assert.Equal("true", result.Payload.Diagnostics["recovered_from_stale_ref"]);
        Assert.Equal("w1e5", result.Payload.Diagnostics["resolved_ref"]);
    }

    private sealed class StubTargetResolver : ITargetResolver
    {
        private readonly bool _success;
        private readonly ToolError? _error;
        private readonly Func<ActionTarget, TargetResolution>? _resolutionFactory;

        public StubTargetResolver(bool success, ToolError? error = null, Func<ActionTarget, TargetResolution>? resolutionFactory = null)
        {
            _success = success;
            _error = error;
            _resolutionFactory = resolutionFactory;
        }

        public ToolResult<ResolvedTarget> Resolve(ActionTarget target)
        {
            if (!_success)
            {
                return new ToolResult<ResolvedTarget>(null, _error!);
            }

            var resolutionSource = target.Ref is null ? "selector" : "ref";
            var resolution = _resolutionFactory?.Invoke(target) ?? new TargetResolution(
                resolutionSource,
                new ElementRef(target.Ref ?? "w1e2"),
                new WindowRef("w1"),
                target.Selector,
                0.95d,
                new Dictionary<string, string?>());

            return new ToolResult<ResolvedTarget>(
                new ResolvedTarget(
                    new RefEntry(
                        resolution.MatchedRef?.Value ?? target.Ref ?? "w1e2",
                        new WindowRef("w1"),
                        "uia",
                        new object(),
                        target.Selector,
                        "snap-stub",
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow),
                    null,
                    resolution),
                null);
        }
    }

    private sealed class StubActionExecutor : IActionExecutor
    {
        private readonly ActionExecutionResult _result;

        public StubActionExecutor()
            : this(new ActionExecutionResult(
                true,
                "uia_pattern",
                new Dictionary<string, string?>(),
                new Dictionary<string, string?>()))
        {
        }

        public StubActionExecutor(ActionExecutionResult result)
        {
            _result = result;
        }

        public ToolResult<ActionExecutionResult> Execute(ActionRequest request, ResolvedTarget target)
        {
            return new ToolResult<ActionExecutionResult>(_result, null);
        }
    }

    private sealed class FailingActionExecutor : IActionExecutor
    {
        private readonly ToolError _error;

        public FailingActionExecutor(ToolError error)
        {
            _error = error;
        }

        public ToolResult<ActionExecutionResult> Execute(ActionRequest request, ResolvedTarget target)
        {
            return new ToolResult<ActionExecutionResult>(null, _error);
        }
    }

    private sealed class SequencedActionExecutor : IActionExecutor
    {
        private readonly ToolErrorCode _errorCode;
        private readonly bool _retryable;
        private readonly int _totalFailures;
        private int _attempts;

        public SequencedActionExecutor(ToolErrorCode errorCode, bool retryable, int totalFailures)
        {
            _errorCode = errorCode;
            _retryable = retryable;
            _totalFailures = totalFailures;
        }

        public ToolResult<ActionExecutionResult> Execute(ActionRequest request, ResolvedTarget target)
        {
            _attempts++;
            if (_attempts <= _totalFailures)
            {
                return new ToolResult<ActionExecutionResult>(null, new ToolError(
                    _errorCode,
                    $"Attempt {_attempts} failed.",
                    _retryable,
                    "Retry the action.",
                    new Dictionary<string, string?>
                    {
                        ["attempt_index"] = _attempts.ToString(),
                    }));
            }

            return new ToolResult<ActionExecutionResult>(new ActionExecutionResult(
                true,
                "input_simulation",
                new Dictionary<string, string?>(),
                new Dictionary<string, string?>()), null);
        }
    }
}
