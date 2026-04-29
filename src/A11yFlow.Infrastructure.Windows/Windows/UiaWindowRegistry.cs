using System.Runtime.InteropServices;
using System.ComponentModel;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;
using FlaUI.UIA3;
using A11yFlow.Core.Abstractions;
using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;

namespace A11yFlow.Infrastructure.Windows.Windows;

public sealed class UiaWindowRegistry : IWindowRegistry, IDisposable
{
    private readonly UIA3Automation _automation = new();
    private readonly IRefRegistry _refRegistry;

    public UiaWindowRegistry(IRefRegistry refRegistry)
    {
        _refRegistry = refRegistry;
    }

    public IReadOnlyList<WindowSummary> ListWindows()
    {
        return RetryUiAutomationCall(() =>
        {
            var activeHandle = NativeMethods.GetForegroundWindow();

            return NativeMethods.EnumerateVisibleTopLevelWindows()
                .Select(handle => TryToSummary(handle, activeHandle))
                .Where(summary => summary is not null)
                .Cast<WindowSummary>()
                .Where(summary => !string.IsNullOrWhiteSpace(summary.Title))
                .ToList();
        });
    }

    public WindowSummary? GetActiveWindow()
    {
        var activeHandle = NativeMethods.GetForegroundWindow();
        if (activeHandle == nint.Zero)
        {
            return null;
        }

        return TryToSummary(activeHandle, activeHandle);
    }

    public nint? GetNativeHandle(WindowRef windowRef)
    {
        return ListWindows()
            .FirstOrDefault(window => window.Ref == windowRef) is { } summary
            ? RetryUiAutomationCall(() => NativeMethods.EnumerateVisibleTopLevelWindows()
                .FirstOrDefault(handle => _refRegistry.GetOrCreateWindowRef(handle) == summary.Ref))
            : null;
    }

    public void Dispose()
    {
        _automation.Dispose();
    }

    private WindowSummary? TryToSummary(nint handle, nint activeHandle)
    {
        if (handle == nint.Zero)
        {
            return null;
        }

        var window = _automation.FromHandle(handle)?.AsWindow();
        if (window is null)
        {
            return null;
        }

        var windowRef = _refRegistry.GetOrCreateWindowRef(handle);

        return new WindowSummary(
            windowRef,
            GetOptionalValue(() => window.Title) ?? string.Empty,
            GetOptionalValue(() => window.Properties.ProcessId.ValueOrDefault),
            GetOptionalValue(() => window.Properties.FrameworkId.ValueOrDefault),
            handle == activeHandle);
    }

    private static T? GetOptionalValue<T>(Func<T> accessor)
    {
        try
        {
            return accessor();
        }
        catch (PropertyNotSupportedException)
        {
            return default;
        }
        catch (COMException)
        {
            return default;
        }
    }

    private static T RetryUiAutomationCall<T>(Func<T> action)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException ex)
            {
                lastError = ex;
            }
            catch (Win32Exception ex)
            {
                lastError = ex;
            }

            Thread.Sleep(100);
        }

        throw lastError!;
    }
}
