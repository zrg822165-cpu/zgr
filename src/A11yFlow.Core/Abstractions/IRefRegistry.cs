using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Core.Abstractions;

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
