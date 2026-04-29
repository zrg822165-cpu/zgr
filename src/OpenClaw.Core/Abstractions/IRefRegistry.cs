using OpenClaw.Core.Models;
using OpenClaw.Core.Refs;

namespace OpenClaw.Core.Abstractions;

public interface IRefRegistry
{
    WindowRef GetOrCreateWindowRef(nint nativeHandle);

    ElementRef CreateElementRef(
        WindowRef windowRef,
        object automationElement,
        string backendSource,
        string snapshotVersion,
        string? selectorHint = null);

    bool TryGetEntry(string reference, out RefEntry? entry);

    void StoreEntry(RefEntry entry);
}
