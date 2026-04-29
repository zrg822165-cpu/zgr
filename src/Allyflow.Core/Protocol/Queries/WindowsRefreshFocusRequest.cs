using Allyflow.Core.Refs;

namespace Allyflow.Protocol.Queries;

public sealed record WindowsRefreshFocusRequest(
    WindowRef? WindowRef,
    int Depth = 2);
