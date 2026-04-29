using Allyflow.Core.Abstractions;
using Allyflow.Core.Actions;
using Allyflow.Core.Models;
using Allyflow.Core.Snapshots;
using Allyflow.Infrastructure.Windows.Actions;
using Allyflow.Infrastructure.Windows.Refs;
using Allyflow.Infrastructure.Windows.Snapshots;
using Allyflow.Infrastructure.Windows.Windows;
using Allyflow.Protocol.Actions;
using Allyflow.Protocol.Queries;

namespace Allyflow.Tests.Integration;

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
