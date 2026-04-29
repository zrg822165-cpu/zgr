namespace OpenClaw.Core.Actions;

// ExecutionPolicy only constrains a single action attempt.
// It is not intended to grow into a task-planning or workflow policy surface.
public sealed record ExecutionPolicy(
    bool AllowFallback,
    int MaxRetries,
    bool RequireVisibility,
    bool AutoActivateWindow,
    bool VerifyAfterAction,
    bool DryRun)
{
    public static ExecutionPolicy Default { get; } = new(
        AllowFallback: true,
        MaxRetries: 0,
        RequireVisibility: false,
        AutoActivateWindow: true,
        VerifyAfterAction: true,
        DryRun: false);
}
