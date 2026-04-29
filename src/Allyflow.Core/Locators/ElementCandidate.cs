using Allyflow.Core.Refs;

namespace Allyflow.Core.Locators;

public sealed record ElementCandidate(
    ElementRef Ref,
    string Role,
    string? Name,
    double Confidence,
    string Strategy,
    string Explanation);
