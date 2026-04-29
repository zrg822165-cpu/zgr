using Allyflow.Core.Abstractions;
using Allyflow.Core.Actions;
using Allyflow.Core.Models;
using Allyflow.Core.Refs;
using Allyflow.Core.Snapshots;

namespace Allyflow.Tests.Unit.Actions;

public sealed class TargetResolverTests
{
    [Fact]
    public void Resolve_StaleRefWithRequestSelector_RecoversUsingSelector()
    {
        var refRegistry = new StubRefRegistry(
            new RefEntry(
                "w1e2",
                new WindowRef("w1"),
                "uia",
                null!,
                "scope:active_window button[name=\"保存\"]",
                "snap-old",
                DateTimeOffset.UtcNow,
                null),
            new RefEntry(
                "w1e5",
                new WindowRef("w1"),
                "uia",
                new object(),
                "scope:active_window button[name=\"保存\"]",
                "snap-new",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));

        var resolver = new TargetResolver(refRegistry, new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = resolver.Resolve(new ActionTarget(
            "w1e2",
            "scope:active_window button[name=\"保存\"]"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("selector_recovery", result.Payload!.Resolution.ResolutionSource);
        Assert.Equal("w1e5", result.Payload.Resolution.MatchedRef!.Value);
        Assert.Equal("request_selector", result.Payload.Resolution.Diagnostics["selector_source"]);
        Assert.Equal("true", result.Payload.Resolution.Diagnostics["recovered_from_stale_ref"]);
        Assert.Equal("true", result.Payload.Resolution.Diagnostics["ref_changed"]);
        Assert.Equal("w1e2", result.Payload.Resolution.Diagnostics["requested_ref"]);
        Assert.Equal("w1e5", result.Payload.Resolution.Diagnostics["resolved_ref"]);
    }

    [Fact]
    public void Resolve_StaleRefWithoutRequestSelector_UsesSelectorHintRecovery()
    {
        var refRegistry = new StubRefRegistry(
            new RefEntry(
                "w1e2",
                new WindowRef("w1"),
                "uia",
                null!,
                "scope:active_window button[name=\"保存\"]",
                "snap-old",
                DateTimeOffset.UtcNow,
                null),
            new RefEntry(
                "w1e5",
                new WindowRef("w1"),
                "uia",
                new object(),
                "scope:active_window button[name=\"保存\"]",
                "snap-new",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));

        var resolver = new TargetResolver(refRegistry, new StubWindowRegistry(), new StubSnapshotBuilder());

        var result = resolver.Resolve(new ActionTarget("w1e2", null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Payload);
        Assert.Equal("selector_recovery", result.Payload!.Resolution.ResolutionSource);
        Assert.Equal("scope:active_window button[name=\"保存\"]", result.Payload.Resolution.SelectorUsed);
        Assert.Equal("selector_hint", result.Payload.Resolution.Diagnostics["selector_source"]);
        Assert.Equal("true", result.Payload.Resolution.Diagnostics["recovered_from_stale_ref"]);
        Assert.Equal("w1e5", result.Payload.Resolution.Diagnostics["resolved_ref"]);
    }

    private sealed class StubRefRegistry : IRefRegistry
    {
        private readonly Dictionary<string, RefEntry> _entries;

        public StubRefRegistry(params RefEntry[] entries)
        {
            _entries = entries.ToDictionary(entry => entry.Ref, entry => entry);
        }

        public WindowRef GetOrCreateWindowRef(nint nativeHandle) => new("w1");

        public ElementRef CreateElementRef(WindowRef windowRef, object automationElement, string backendSource, string snapshotVersion, string? selectorHint = null)
            => throw new NotSupportedException();

        public bool TryGetEntry(string reference, out RefEntry? entry)
        {
            var found = _entries.TryGetValue(reference, out var stored);
            entry = stored;
            return found;
        }

        public void StoreEntry(RefEntry entry)
        {
            _entries[entry.Ref] = entry;
        }
    }

    private sealed class StubWindowRegistry : IWindowRegistry
    {
        private readonly WindowSummary _window = new(new WindowRef("w1"), "设置", 1234, "WPF", true);

        public IReadOnlyList<WindowSummary> ListWindows() => [_window];

        public WindowSummary? GetActiveWindow() => _window;

        public nint? GetNativeHandle(WindowRef windowRef) => 1;
    }

    private sealed class StubSnapshotBuilder : ISnapshotBuilder
    {
        public SnapshotResult BuildActiveWindowSnapshot() => BuildWindowSnapshot(new WindowRef("w1"));

        public SnapshotResult BuildWindowSnapshot(WindowRef windowRef)
        {
            var root = new ElementNode(
                new ElementRef("w1e1"),
                "window",
                "设置",
                null,
                null,
                null,
                ["visible"],
                Array.Empty<string>(),
                [
                    new ElementNode(
                        new ElementRef("w1e5"),
                        "button",
                        "保存",
                        null,
                        null,
                        null,
                        ["enabled", "visible"],
                        ["invoke"],
                        Array.Empty<ElementNode>())
                ]);

            return new SnapshotResult(windowRef, "snap-new", root, "summary", new Dictionary<string, string?>());
        }
    }
}
