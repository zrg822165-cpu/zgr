namespace OpenClaw.Core.Locators;

public sealed record LocateResult(
    LocateStatus Status,
    ElementCandidate? BestMatch,
    IReadOnlyList<ElementCandidate> Candidates,
    string Explain,
    IReadOnlyDictionary<string, string?> Diagnostics);
