namespace A11yFlow.Core.Actions;

public sealed record VerificationResult(
    bool Passed,
    string Type,
    string Message,
    IReadOnlyDictionary<string, string?> Diagnostics);
