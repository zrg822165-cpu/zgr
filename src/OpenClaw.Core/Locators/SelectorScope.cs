namespace OpenClaw.Core.Locators;

public sealed record SelectorScope(SelectorScopeKind Kind, string? WindowName = null);
