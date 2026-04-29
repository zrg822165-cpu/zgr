using System.Diagnostics;
using A11yFlow.Core.Actions;
using A11yFlow.Core.Locators;
using A11yFlow.Core.Models;
using A11yFlow.Core.Snapshots;
using A11yFlow.Protocol.Actions;
using A11yFlow.Protocol.Queries;

namespace A11yFlow.Tests.Integration;

internal sealed class RealAppSession : IDisposable
{
    private readonly Process _process;

    public RealAppSession(Process process)
    {
        _process = process;
    }

    public void Dispose()
    {
        try
        {
            if (_process.HasExited)
            {
                return;
            }

            _process.CloseMainWindow();
            if (!_process.WaitForExit(2000))
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(2000);
            }
        }
        catch
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(2000);
            }
        }
    }
}

internal static class RealAppTestDriver
{
    public static RealAppSession Launch(string fileName)
    {
        var startInfo = fileName.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("ms-settings:", StringComparison.OrdinalIgnoreCase)
            ? new ProcessStartInfo("explorer.exe", fileName)
            {
                UseShellExecute = true,
            }
            : Directory.Exists(fileName)
                ? new ProcessStartInfo("explorer.exe", $"\"{fileName}\"")
                {
                    UseShellExecute = true,
                }
            : new ProcessStartInfo(fileName)
            {
                UseShellExecute = true,
            };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        return new RealAppSession(process);
    }

    public static WindowSummary WaitForNewWindow(
        QueryToolService queryService,
        HashSet<string> existingWindowRefs,
        Func<WindowSummary, bool> match)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var windows = queryService.WindowsList();
            var window = windows.Windows.FirstOrDefault(candidate =>
                !existingWindowRefs.Contains(candidate.Ref.Value) &&
                !string.IsNullOrWhiteSpace(candidate.Title) &&
                match(candidate));

            if (window is not null)
            {
                return window;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException("Timed out waiting for a new matching window.");
    }

    public static SnapshotResult SnapshotWindow(QueryToolService queryService, WindowSummary window)
    {
        var snapshot = queryService.WindowsSnapshot(new WindowsSnapshotRequest(window.Ref));
        Assert.True(snapshot.IsSuccess, snapshot.Error?.Message ?? "windows_snapshot failed");
        Assert.NotNull(snapshot.Payload);
        Assert.Equal("windows_snapshot", snapshot.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", snapshot.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", snapshot.Payload.Diagnostics["primary_interface"]);
        return snapshot.Payload;
    }

    public static LocateResult Locate(QueryToolService queryService, string selector)
    {
        var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
        Assert.True(locate.IsSuccess, locate.Error?.Message ?? "windows_locate failed");
        Assert.NotNull(locate.Payload);
        Assert.NotNull(locate.Payload!.BestMatch);
        Assert.Equal("windows_locate", locate.Payload.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", locate.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", locate.Payload.Diagnostics["primary_interface"]);
        Assert.Equal(selector, locate.Payload.Diagnostics["selector_used"]);
        return locate.Payload;
    }

    public static ActionResult Act(
        ActionToolService actionService,
        string requestId,
        string targetRef,
        string action,
        IReadOnlyDictionary<string, object?> arguments,
        ExecutionPolicy policy,
        ExpectedOutcome expectedOutcome)
    {
        var result = actionService.WindowsAct(new ActRequest(
            requestId,
            new ActionTarget(targetRef, null),
            action,
            arguments,
            policy,
            expectedOutcome,
            5000));

        Assert.True(result.IsSuccess, result.Error?.Message ?? result.Payload?.Error?.Message ?? "windows_act failed");
        Assert.NotNull(result.Payload);
        Assert.Equal("windows_act", result.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
        Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
        Assert.Equal(action, result.Payload.Diagnostics["action_name"]);
        return result.Payload;
    }

    public static void WaitUntil(Func<bool> condition, string timeoutMessage, int timeoutMilliseconds = 5000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (condition())
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new TimeoutException(timeoutMessage);
    }
}
