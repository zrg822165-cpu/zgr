using OpenClaw.Core.Refs;

namespace OpenClaw.Protocol.Queries;

public sealed record WindowsRefreshFocusRequest(
    WindowRef? WindowRef,
    int Depth = 2);
