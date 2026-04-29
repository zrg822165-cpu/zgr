using System.Collections.Concurrent;
using Allyflow.Core.Abstractions;
using Allyflow.Core.Models;
using Allyflow.Core.Refs;

namespace Allyflow.Infrastructure.Windows.Refs;

public sealed class InMemoryRefRegistry : IRefRegistry
{
    private readonly ConcurrentDictionary<nint, WindowRef> _windowRefs = new();
    private readonly ConcurrentDictionary<string, RefEntry> _entries = new();
    private readonly ConcurrentDictionary<string, int> _elementCounters = new();
    private int _windowCounter;

    public WindowRef GetOrCreateWindowRef(nint nativeHandle)
    {
        return _windowRefs.GetOrAdd(nativeHandle, _ => new WindowRef($"w{Interlocked.Increment(ref _windowCounter)}"));
    }

    public ElementRef CreateElementRef(
        WindowRef windowRef,
        object automationElement,
        string backendSource,
        string snapshotVersion,
        string? selectorHint = null)
    {
        var elementIndex = _elementCounters.AddOrUpdate(windowRef.Value, 1, static (_, current) => current + 1);
        var elementRef = new ElementRef($"{windowRef.Value}e{elementIndex}");

        var entry = new RefEntry(
            elementRef.Value,
            windowRef,
            backendSource,
            automationElement,
            selectorHint,
            snapshotVersion,
            DateTimeOffset.UtcNow,
            null);

        _entries[elementRef.Value] = entry;
        return elementRef;
    }

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
