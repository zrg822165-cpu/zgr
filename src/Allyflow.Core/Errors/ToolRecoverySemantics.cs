namespace Allyflow.Core.Errors;

public static class ToolRecoverySemantics
{
    public static IReadOnlyDictionary<string, string?> For(ToolErrorCode errorCode, ToolErrorCode? underlyingErrorCode = null)
    {
        var effectiveCode = underlyingErrorCode ?? errorCode;

        return effectiveCode switch
        {
            ToolErrorCode.TargetAmbiguous => new Dictionary<string, string?>
            {
                ["recovery_kind"] = "ambiguity_resolution",
                ["recovery_hint"] = "describe_candidates_then_refine",
                ["recovery_target_tool"] = "windows_describe_ref",
                ["recovery_selector_refinement"] = "role|automation_id|name|structure",
                ["suggested_next_step_kind"] = "describe_candidates_then_refine",
            },
            ToolErrorCode.RefNotFound or ToolErrorCode.RefStale or ToolErrorCode.TargetStale or ToolErrorCode.TargetNotFound => new Dictionary<string, string?>
            {
                ["recovery_kind"] = "target_recovery",
                ["recovery_hint"] = "refresh_snapshot_then_relocate",
                ["recovery_target_tool"] = "windows_snapshot",
                ["suggested_next_step_kind"] = "refresh_snapshot_then_relocate",
            },
            ToolErrorCode.NoActiveWindow or ToolErrorCode.WindowNotFound => new Dictionary<string, string?>
            {
                ["recovery_kind"] = "surface_recovery",
                ["recovery_hint"] = "refresh_window_context",
                ["recovery_target_tool"] = "windows_list",
                ["suggested_next_step_kind"] = "refresh_window_context",
            },
            ToolErrorCode.VerificationFailed => new Dictionary<string, string?>
            {
                ["recovery_kind"] = "verification_recheck",
                ["recovery_hint"] = "refresh_snapshot_and_inspect_effect",
                ["recovery_target_tool"] = "windows_snapshot",
                ["suggested_next_step_kind"] = "refresh_snapshot_and_inspect_effect",
            },
            _ => new Dictionary<string, string?>(),
        };
    }
}
