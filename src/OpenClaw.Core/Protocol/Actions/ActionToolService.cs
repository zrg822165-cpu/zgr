using OpenClaw.Core.Abstractions;
using OpenClaw.Core.Actions;
using OpenClaw.Core.Errors;
using OpenClaw.Core.Models;

namespace OpenClaw.Protocol.Actions;

public sealed class ActionToolService
{
    private readonly ITargetResolver _targetResolver;
    private readonly IActionExecutor _actionExecutor;

    public ActionToolService(ITargetResolver targetResolver, IActionExecutor actionExecutor)
    {
        _targetResolver = targetResolver;
        _actionExecutor = actionExecutor;
    }

    public ToolResult<ActionResult> WindowsAct(ActRequest request)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var normalized = Normalize(request);

        var resolved = _targetResolver.Resolve(normalized.Target);
        if (!resolved.IsSuccess)
        {
            var normalizedError = NormalizeError(normalized, resolved.Error!, string.Empty, null, 0);
            return new ToolResult<ActionResult>(
                BuildFailureResult(normalized, normalizedError, startedAt, string.Empty, null, null, 0),
                normalizedError);
        }

        if (normalized.ExecutionPolicy.DryRun)
        {
            var finishedAt = DateTimeOffset.UtcNow;
            var dryRunDiagnostics = ActionDiagnostics.ForAction(
                "windows_act",
                normalized,
                resolved.Payload!.Resolution,
                "dry_run",
                resolved.Payload!.Resolution.Diagnostics,
                new Dictionary<string, string?>
                {
                    ["selector_source"] = GetResolutionDiagnostic(resolved.Payload.Resolution, "selector_source"),
                    ["recovered_from_stale_ref"] = GetResolutionDiagnostic(resolved.Payload.Resolution, "recovered_from_stale_ref"),
                    ["resolved_ref"] = GetResolutionDiagnostic(resolved.Payload.Resolution, "resolved_ref") ?? resolved.Payload.Resolution.MatchedRef?.Value,
                    ["preview_kind"] = "execution_only",
                    ["preview_action"] = normalized.Action,
                    ["preview_execution_path"] = BuildPlannedExecutionPath(normalized.Action, normalized.ExecutionPolicy),
                    ["allow_fallback"] = normalized.ExecutionPolicy.AllowFallback.ToString().ToLowerInvariant(),
                    ["require_visibility"] = normalized.ExecutionPolicy.RequireVisibility.ToString().ToLowerInvariant(),
                    ["auto_activate_window"] = normalized.ExecutionPolicy.AutoActivateWindow.ToString().ToLowerInvariant(),
                });

            var dryRunResult = new ActionResult(
                normalized.RequestId,
                true,
                ActionStatus.Success,
                resolved.Payload!.Resolution,
                normalized.Action,
                normalized.Arguments,
                "dry_run",
                0,
                new Dictionary<string, string?>
                {
                    ["preview_action"] = normalized.Action,
                    ["preview_execution_path"] = BuildPlannedExecutionPath(normalized.Action, normalized.ExecutionPolicy),
                },
                BuildVerification(normalized, true, "Execution preview completed without running the action."),
                null,
                dryRunDiagnostics,
                new ActionTiming(startedAt, finishedAt, (long)(finishedAt - startedAt).TotalMilliseconds));

            return new ToolResult<ActionResult>(dryRunResult, null);
        }

        var maxAttempts = Math.Max(1, normalized.ExecutionPolicy.MaxRetries + 1);
        var attemptDiagnostics = new Dictionary<string, string?>();
        ToolResult<ActionExecutionResult>? execution = null;
        var retriesUsed = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            execution = _actionExecutor.Execute(normalized, resolved.Payload!);
            RecordAttempt(attemptDiagnostics, attempt, execution);

            if (execution.IsSuccess)
            {
                retriesUsed = attempt - 1;
                break;
            }

            retriesUsed = attempt - 1;
            if (attempt == maxAttempts || execution.Error is null || !execution.Error.Retryable)
            {
                break;
            }
        }

        if (execution is null)
        {
            var unexpectedError = new ToolError(
                ToolErrorCode.BackendError,
                "Action execution produced no result.",
                true,
                "Retry the action after refreshing the target snapshot.",
                new Dictionary<string, string?>());
            var normalizedUnexpectedError = NormalizeError(normalized, unexpectedError, string.Empty, resolved.Payload!.Resolution, retriesUsed);

            return new ToolResult<ActionResult>(
                BuildFailureResult(normalized, normalizedUnexpectedError, startedAt, string.Empty, resolved.Payload!.Resolution, null, retriesUsed),
                normalizedUnexpectedError);
        }

        if (!execution.IsSuccess)
        {
            var finalError = execution.Error!;
            if (finalError.Retryable && maxAttempts > 1)
            {
                finalError = new ToolError(
                    ToolErrorCode.FallbackExhausted,
                    $"Action '{normalized.Action}' exhausted its retry budget after {maxAttempts} attempts.",
                    false,
                    "Refresh the snapshot, inspect diagnostics, and retry with a more specific target or different policy.",
                    MergeDiagnostics(finalError.Diagnostics, attemptDiagnostics, new Dictionary<string, string?>
                    {
                        ["attempt_count"] = maxAttempts.ToString(),
                        ["retry_count"] = retriesUsed.ToString(),
                        ["final_error_code"] = execution.Error!.Code.ToString(),
                    }));
            }
            else
            {
                finalError = new ToolError(
                    finalError.Code,
                    finalError.Message,
                    finalError.Retryable,
                    finalError.SuggestedNextStep,
                    MergeDiagnostics(finalError.Diagnostics, attemptDiagnostics, new Dictionary<string, string?>
                    {
                        ["attempt_count"] = (retriesUsed + 1).ToString(),
                        ["retry_count"] = retriesUsed.ToString(),
                    }));
            }

            var normalizedFinalError = NormalizeError(normalized, finalError, string.Empty, resolved.Payload!.Resolution, retriesUsed);

            return new ToolResult<ActionResult>(
                BuildFailureResult(normalized, normalizedFinalError, startedAt, string.Empty, resolved.Payload!.Resolution, null, retriesUsed),
                normalizedFinalError);
        }

        var verification = Verify(normalized, execution.Payload!);
        var finished = DateTimeOffset.UtcNow;
        var success = execution.Payload!.Success && verification.Passed;
        var error = success
            ? null
            : NormalizeError(
                normalized,
                new ToolError(
                ToolErrorCode.VerificationFailed,
                verification.Message,
                true,
                "Refresh the snapshot and inspect the observed effect before retrying.",
                verification.Diagnostics),
                execution.Payload.ExecutionPath,
                resolved.Payload!.Resolution,
                retriesUsed);

        var result = new ActionResult(
            normalized.RequestId,
            success,
            success ? ActionStatus.Success : ActionStatus.PartialSuccess,
            resolved.Payload!.Resolution,
            normalized.Action,
            normalized.Arguments,
            execution.Payload.ExecutionPath,
            retriesUsed,
            execution.Payload.ObservedEffect,
            verification,
            error,
            ActionDiagnostics.ForAction(
                "windows_act",
                normalized,
                resolved.Payload!.Resolution,
                execution.Payload.ExecutionPath,
                resolved.Payload.Resolution.Diagnostics,
                new Dictionary<string, string?>
                {
                    ["selector_source"] = GetResolutionDiagnostic(resolved.Payload.Resolution, "selector_source"),
                    ["recovered_from_stale_ref"] = GetResolutionDiagnostic(resolved.Payload.Resolution, "recovered_from_stale_ref"),
                    ["resolved_ref"] = GetResolutionDiagnostic(resolved.Payload.Resolution, "resolved_ref") ?? resolved.Payload.Resolution.MatchedRef?.Value,
                    ["attempt_count"] = (retriesUsed + 1).ToString(),
                    ["retry_count"] = retriesUsed.ToString(),
                },
                execution.Payload.Diagnostics,
                attemptDiagnostics,
                verification.Diagnostics),
            new ActionTiming(startedAt, finished, (long)(finished - startedAt).TotalMilliseconds));

        return new ToolResult<ActionResult>(result, error);
    }

    private static ActionRequest Normalize(ActRequest request)
    {
        return new ActionRequest(
            string.IsNullOrWhiteSpace(request.RequestId) ? $"req-{Guid.NewGuid():N}" : request.RequestId,
            request.Target,
            request.Action,
            request.Arguments,
            request.ExecutionPolicy ?? ExecutionPolicy.Default,
            request.ExpectedOutcome,
            request.TimeoutMs ?? 5000);
    }

    private static VerificationResult Verify(ActionRequest request, ActionExecutionResult execution)
    {
        if (request.ExpectedOutcome is null ||
            string.Equals(request.ExpectedOutcome.Type, "no_verification", StringComparison.OrdinalIgnoreCase) ||
            !request.ExecutionPolicy.VerifyAfterAction)
        {
            return BuildVerification(request, true, "Verification skipped by policy.");
        }

        if (!execution.Success)
        {
            return new VerificationResult(
                false,
                request.ExpectedOutcome.Type,
                $"Action execution did not satisfy expected outcome '{request.ExpectedOutcome.Type}'.",
                new Dictionary<string, string?>(execution.ObservedEffect));
        }

        var observed = execution.ObservedEffect;
        var conditions = request.ExpectedOutcome.Conditions;
        var expectedType = request.ExpectedOutcome.Type;

        return expectedType.ToLowerInvariant() switch
        {
            "text_updated" => VerifyExpectedValue(request, observed, "text_updated", "text"),
            "property_change" => VerifyPropertyChange(request, observed, conditions),
            "focus_change" => VerifyFlagOutcome(request, observed, "focus_changed"),
            "focus_changed" => VerifyFlagOutcome(request, observed, "focus_changed"),
            "window_opened" => VerifyFlagOutcome(request, observed, "window_opened"),
            "window_closed" => VerifyFlagOutcome(request, observed, "window_closed"),
            _ => new VerificationResult(
                true,
                request.ExpectedOutcome.Type,
                $"Observed expected outcome type '{request.ExpectedOutcome.Type}'.",
                new Dictionary<string, string?>(observed)),
        };
    }

    private static VerificationResult BuildVerification(ActionRequest request, bool passed, string message)
    {
        return new VerificationResult(
            passed,
            request.ExpectedOutcome?.Type ?? "no_verification",
            message,
            new Dictionary<string, string?>());
    }

    private static ActionResult BuildFailureResult(
        ActionRequest request,
        ToolError error,
        DateTimeOffset startedAt,
        string executionPath,
        TargetResolution? resolution,
        IReadOnlyDictionary<string, string?>? observedEffect,
        int retries)
    {
        var finished = DateTimeOffset.UtcNow;
        return new ActionResult(
            request.RequestId,
            false,
            error.Code == ToolErrorCode.TargetAmbiguous ? ActionStatus.Ambiguous : ActionStatus.Failed,
            resolution,
            request.Action,
            request.Arguments,
            executionPath,
            retries,
            observedEffect ?? new Dictionary<string, string?>(),
            BuildVerification(request, false, error.Message),
            error,
            ActionDiagnostics.ForAction(
                "windows_act",
                request,
                resolution,
                executionPath,
                error.Diagnostics,
                new Dictionary<string, string?>
                {
                    ["retry_count"] = retries.ToString(),
                }),
            new ActionTiming(startedAt, finished, (long)(finished - startedAt).TotalMilliseconds));
    }

    private static ToolError NormalizeError(
        ActionRequest request,
        ToolError error,
        string executionPath,
        TargetResolution? resolution,
        int retries)
    {
        ToolErrorCode? underlyingErrorCode = error.Code == ToolErrorCode.FallbackExhausted
            && error.Diagnostics.TryGetValue("final_error_code", out var finalErrorCode)
            && Enum.TryParse<ToolErrorCode>(finalErrorCode, out var parsedFinalErrorCode)
                ? parsedFinalErrorCode
                : null;

        var recoverySemantics = ToolRecoverySemantics.For(error.Code, underlyingErrorCode);

        return new ToolError(
            error.Code,
            error.Message,
            error.Retryable,
            error.SuggestedNextStep,
            ActionDiagnostics.ForError(
                "windows_act",
                request,
                resolution,
                executionPath,
                error.Diagnostics,
                recoverySemantics,
                new Dictionary<string, string?>
                {
                    ["retry_count"] = retries.ToString(),
                }));
    }

    private static VerificationResult VerifyExpectedValue(ActionRequest request, IReadOnlyDictionary<string, string?> observed, string observedKey, string argumentKey)
    {
        observed.TryGetValue(observedKey, out var actualValue);
        var expectedValue = request.ExpectedOutcome?.Conditions.TryGetValue("value", out var explicitValue) == true
            ? explicitValue?.ToString()
            : request.Arguments.TryGetValue(argumentKey, out var argumentValue)
                ? argumentValue?.ToString()
                : null;

        var passed = !string.IsNullOrWhiteSpace(expectedValue) && string.Equals(actualValue, expectedValue, StringComparison.Ordinal);
        return new VerificationResult(
            passed,
            request.ExpectedOutcome!.Type,
            passed
                ? $"Observed expected value '{expectedValue}'."
                : $"Expected value '{expectedValue}' but observed '{actualValue ?? "<null>"}'.",
            new Dictionary<string, string?>(observed)
            {
                ["expected_value"] = expectedValue,
                ["observed_value"] = actualValue,
            });
    }

    private static VerificationResult VerifyPropertyChange(ActionRequest request, IReadOnlyDictionary<string, string?> observed, IReadOnlyDictionary<string, object?> conditions)
    {
        var propertyName = conditions.TryGetValue("property", out var property) ? property?.ToString() : null;
        var expectedValue = conditions.TryGetValue("value", out var explicitValue)
            ? explicitValue?.ToString()
            : TryReadExpectedPropertyValue(request.Arguments, propertyName);

        var observedValue = propertyName is null ? null : TryReadObservedPropertyValue(observed, propertyName);
        var passed = propertyName is not null && string.Equals(observedValue, expectedValue, StringComparison.OrdinalIgnoreCase);

        return new VerificationResult(
            passed,
            request.ExpectedOutcome!.Type,
            passed
                ? $"Observed property '{propertyName}' changed to '{expectedValue}'."
                : $"Expected property '{propertyName}' to be '{expectedValue}', observed '{observedValue ?? "<null>"}'.",
            new Dictionary<string, string?>(observed)
            {
                ["expected_property"] = propertyName,
                ["expected_value"] = expectedValue,
                ["observed_value"] = observedValue,
            });
    }

    private static VerificationResult VerifyFlagOutcome(ActionRequest request, IReadOnlyDictionary<string, string?> observed, string key)
    {
        observed.TryGetValue(key, out var rawValue);
        var passed = string.Equals(rawValue, bool.TrueString, StringComparison.OrdinalIgnoreCase) || string.Equals(rawValue, "true", StringComparison.OrdinalIgnoreCase);

        return new VerificationResult(
            passed,
            request.ExpectedOutcome!.Type,
            passed
                ? $"Observed '{key}' after execution."
                : $"Expected '{key}' after execution, but it was not observed.",
            new Dictionary<string, string?>(observed));
    }

    private static string? TryReadExpectedPropertyValue(IReadOnlyDictionary<string, object?> arguments, string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return null;
        }

        return propertyName.ToLowerInvariant() switch
        {
            "toggle_state" => arguments.TryGetValue("target_state", out var targetState) ? targetState?.ToString()?.ToLowerInvariant() : null,
            "expand_state" => arguments.TryGetValue("target_state", out var expandState) ? expandState?.ToString() : null,
            "range_value" => arguments.TryGetValue("target_value", out var targetValue) ? targetValue?.ToString() : null,
            "selected_item" => arguments.TryGetValue("option_text", out var optionText) ? optionText?.ToString() : null,
            _ => arguments.TryGetValue(propertyName, out var value) ? value?.ToString() : null,
        };
    }

    private static string? TryReadObservedPropertyValue(IReadOnlyDictionary<string, string?> observed, string propertyName)
    {
        return propertyName.ToLowerInvariant() switch
        {
            "toggle_state" when observed.TryGetValue("toggle_state", out var toggleValue) => toggleValue,
            "expand_state" when observed.TryGetValue("expand_state", out var expandValue) => expandValue,
            "range_value" when observed.TryGetValue("range_value", out var rangeValue) => rangeValue,
            "selected_item" when observed.TryGetValue("selected_item", out var selectedItem) => selectedItem,
            _ when observed.TryGetValue(propertyName, out var value) => value,
            _ => null,
        };
    }

    private static string BuildPlannedExecutionPath(string action, ExecutionPolicy policy)
    {
        return action.ToLowerInvariant() switch
        {
            "click" or "invoke" => policy.AllowFallback ? "uia_pattern -> input_simulation" : "uia_pattern",
            "press_keys" => "input_simulation",
            _ => "uia_pattern",
        };
    }

    private static void RecordAttempt(IDictionary<string, string?> diagnostics, int attempt, ToolResult<ActionExecutionResult> execution)
    {
        if (execution.IsSuccess)
        {
            diagnostics[$"attempt_{attempt}_status"] = "success";
            diagnostics[$"attempt_{attempt}_execution_path"] = execution.Payload!.ExecutionPath;
            return;
        }

        diagnostics[$"attempt_{attempt}_status"] = "failed";
        diagnostics[$"attempt_{attempt}_error_code"] = execution.Error?.Code.ToString();
        diagnostics[$"attempt_{attempt}_retryable"] = execution.Error?.Retryable.ToString().ToLowerInvariant();
    }

    private static string? GetResolutionDiagnostic(TargetResolution resolution, string key)
    {
        return resolution.Diagnostics.TryGetValue(key, out var value) ? value : null;
    }

    private static IReadOnlyDictionary<string, string?> MergeDiagnostics(params IReadOnlyDictionary<string, string?>[] groups)
        => ActionDiagnostics.Merge(groups);
}
