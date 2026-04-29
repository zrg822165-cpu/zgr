using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Locators;

public sealed record ElementCandidate(
    ElementRef Ref,
    string Role,
    string? Name,
    double Confidence,
    string Strategy,
    string Explanation);
