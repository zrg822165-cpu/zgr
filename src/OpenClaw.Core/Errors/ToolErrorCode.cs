namespace OpenClaw.Core.Errors;

public enum ToolErrorCode
{
    NoActiveWindow,
    WindowNotFound,
    RefNotFound,
    RefStale,
    TargetStale,
    TargetNotFound,
    TargetAmbiguous,
    TargetNotActionable,
    ActionNotSupported,
    VisibilityRequired,
    WindowNotActive,
    ExecutionTimeout,
    VerificationFailed,
    FallbackBlocked,
    FallbackExhausted,
    BackendError,
    InvalidArgument,
}
