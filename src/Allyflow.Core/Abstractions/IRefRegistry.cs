using Allyflow.Core.Models;
using Allyflow.Core.Refs;

namespace Allyflow.Core.Abstractions;

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
