using A11yFlow.Core.Abstractions;
using A11yFlow.Core.Actions;
using A11yFlow.Core.Models;
using A11yFlow.Core.Snapshots;
using A11yFlow.Infrastructure.Windows.Actions;
using A11yFlow.Infrastructure.Windows.Refs;
using A11yFlow.Infrastructure.Windows.Snapshots;
using A11yFlow.Infrastructure.Windows.Windows;
using A11yFlow.Protocol.Actions;
using A11yFlow.Protocol.Queries;

namespace A11yFlow.Tests.Integration;

internal sealed class IntegrationTestRuntime : IDisposable
{
    private readonly UiaWindowRegistry _windowRegistry;
    private readonly UiaSnapshotBuilder _snapshotBuilder;

    public QueryToolService QueryService { get; }

    public ActionToolService ActionService { get; }

    public IntegrationTestRuntime()
    {
        IRefRegistry refRegistry = new InMemoryRefRegistry();
        _windowRegistry = new UiaWindowRegistry(refRegistry);
        _snapshotBuilder = new UiaSnapshotBuilder(_windowRegistry, refRegistry, new SnapshotTextFormatter());

        QueryService = new QueryToolService(_windowRegistry, _snapshotBuilder);

        var targetResolver = new TargetResolver(refRegistry, _windowRegistry, _snapshotBuilder);
        ActionService = new ActionToolService(targetResolver, new UiaActionExecutor(_windowRegistry));
    }

    public WindowSummary? WaitForWindow(string windowTitle)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var result = QueryService.WindowsList();
            var match = result.Windows.FirstOrDefault(window => string.Equals(window.Title, windowTitle, StringComparison.Ordinal));
            if (match is not null)
            {
                return match;
            }

            Thread.Sleep(100);
        }

        return null;
    }

    public void Dispose()
    {
        _snapshotBuilder.Dispose();
        _windowRegistry.Dispose();
    }
}
