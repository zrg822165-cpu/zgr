using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Locators;

public sealed record ElementCandidate(
    ElementRef Ref,
    string Role,
    string? Name,
    double Confidence,
    string Strategy,
    string Explanation);
