using A11yFlow.Core.Refs;

namespace A11yFlow.Protocol.Queries;

public sealed record WindowsRefreshFocusRequest(
    WindowRef? WindowRef,
    int Depth = 2);
