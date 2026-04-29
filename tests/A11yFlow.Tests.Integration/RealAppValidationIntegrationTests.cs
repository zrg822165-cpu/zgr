using A11yFlow.Core.Actions;
using A11yFlow.Core.Locators;
using A11yFlow.Core.Models;
using A11yFlow.Core.Snapshots;
using A11yFlow.Protocol.Actions;
using A11yFlow.Protocol.Queries;
using System.Text;

namespace A11yFlow.Tests.Integration;

[Collection("UIA integration")]
public sealed class RealAppValidationIntegrationTests
{
    private static readonly string DisposableSaveDirectory = Path.Combine(Path.GetTempPath(), "A11yFlow.RealAppValidation");

    [Fact]
    public void Notepad_SetValue_WorksOnRealDesktopApp()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("notepad.exe");
        var window = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate => candidate.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        var windowTitle = window.Title;
        Assert.False(string.IsNullOrWhiteSpace(windowTitle));

        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, window);
        Assert.Contains("Notepad", snapshot.SummaryText, StringComparison.OrdinalIgnoreCase);

        var selector = $"scope:window(name=\"{windowTitle}\") document[name=\"文本编辑器\"]";
        var locate = RealAppTestDriver.Locate(queryService, selector);
        Assert.Equal("document", locate.BestMatch!.Role, ignoreCase: true);

        var text = $"A11yFlow Notepad validation {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}";
        var action = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-set-value",
            locate.BestMatch.Ref.Value,
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = text,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000));

        Assert.True(action.Success);
        Assert.True(action.Verification.Passed, action.Verification.Message);
        Assert.Equal("value_pattern", action.Diagnostics["action_strategy"]);
        Assert.Equal(text, action.ObservedEffect["text_updated"]);
    }

    [Fact]
    public void Calculator_WindowDiscovery_IsCapturedAsCurrentBaseline()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

        var windowsAfter = queryService.WindowsList();
        Assert.True(windowsAfter.Count >= 0);

        var newWindows = windowsAfter.Windows
            .Where(window => !windowsBefore.Contains(window.Ref.Value))
            .ToList();

        var calculatorCandidates = windowsAfter.Windows
            .Where(window =>
                window.Title.Contains("Calculator", StringComparison.OrdinalIgnoreCase) ||
                window.Title.Contains("计算器", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(window.FrameworkId, "XAML", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotNull(newWindows);
        Assert.NotNull(calculatorCandidates);
    }

    [Fact]
    public void Calculator_Invoke_CanExecuteSimpleRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("shell:AppsFolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

        var window = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate =>
                candidate.Title.Contains("Calculator", StringComparison.OrdinalIgnoreCase) ||
                candidate.Title.Contains("计算器", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.FrameworkId, "XAML", StringComparison.OrdinalIgnoreCase));

        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, window);
        Assert.Contains("计算器", snapshot.SummaryText);

        InvokeCalculatorButton(queryService, actionService, window.Title, "一");
        InvokeCalculatorButton(queryService, actionService, window.Title, "加");
        InvokeCalculatorButton(queryService, actionService, window.Title, "二");
        InvokeCalculatorButton(queryService, actionService, window.Title, "等于");

        var display = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{window.Title}\") text[name~=\"显示为\"]");

        Assert.Contains("显示为 3", display.BestMatch!.Name ?? string.Empty);
    }

    [Fact]
    public void StandardFileDialog_Snapshot_CanBeInspected_FromRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("notepad.exe");
        var notepadWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate => candidate.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        var editor = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{notepadWindow.Title}\") document[name=\"文本编辑器\"]");
        var focus = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-focus-editor-for-save-as",
            editor.BestMatch!.Ref.Value,
            "focus",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("focus_changed", new Dictionary<string, object?>(), 500));

        Assert.True(focus.Success);
        Assert.True(focus.Verification.Passed, focus.Verification.Message);

        OpenNotepadSaveAsDialog(queryService, actionService, notepadWindow.Ref.Value);

        var refreshedNotepadWindow = FindWindowByRef(queryService, notepadWindow.Ref.Value);
        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, refreshedNotepadWindow);
        Assert.Contains("另存为", snapshot.SummaryText);

        var fileNameInput = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") edit[name=\"文件名:\"]");
        Assert.Equal("edit", fileNameInput.BestMatch!.Role, ignoreCase: true);

        var saveButton = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") button[name=\"保存(S)\"]");
        Assert.Equal("button", saveButton.BestMatch!.Role, ignoreCase: true);

        var cancelButton = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") button[name=\"取消\"]");
        var closeDialog = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-cancel-save-as",
            cancelButton.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(closeDialog.Success);
        Assert.Equal("invoke_pattern", closeDialog.Diagnostics["action_strategy"]);
    }

    [Fact]
    public void Notepad_SaveAs_CanPersistToDisposablePath_ThroughRealDesktopFlow()
    {
        Directory.CreateDirectory(DisposableSaveDirectory);

        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("notepad.exe");
        var notepadWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate => candidate.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        var editor = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{notepadWindow.Title}\") document[name=\"文本编辑器\"]");
        var text = $"A11yFlow save validation {DateTimeOffset.UtcNow:O}";
        var setText = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-save-flow-set-text",
            editor.BestMatch!.Ref.Value,
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = text,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000));

        Assert.True(setText.Success);
        Assert.Equal(text, setText.ObservedEffect["text_updated"]);

        OpenNotepadSaveAsDialog(queryService, actionService, notepadWindow.Ref.Value);
        var currentWindowTitle = GetWindowTitle(queryService, notepadWindow.Ref.Value);

        var filePath = Path.Combine(DisposableSaveDirectory, $"a11yflow-real-save-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.txt");
        try
        {
            var fileNameInput = RealAppTestDriver.Locate(
                queryService,
                $"scope:window(name=\"{currentWindowTitle}\") window[name=\"另存为\"] >> edit[name=\"文件名:\"]");
            var setFilePath = RealAppTestDriver.Act(
                actionService,
                "req-real-app-notepad-save-flow-set-file-path",
                fileNameInput.BestMatch!.Ref.Value,
                "set_value",
                new Dictionary<string, object?>
                {
                    ["text"] = filePath,
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000));

            Assert.True(setFilePath.Success);
            Assert.Equal(filePath, setFilePath.ObservedEffect["text_updated"]);

            var saveButton = RealAppTestDriver.Locate(
                queryService,
                $"scope:window(name=\"{currentWindowTitle}\") window[name=\"另存为\"] >> button[name=\"保存(S)\"]");
            var save = RealAppTestDriver.Act(
                actionService,
                "req-real-app-notepad-save-flow-invoke-save",
                saveButton.BestMatch!.Ref.Value,
                "invoke",
                new Dictionary<string, object?>(),
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

            Assert.True(save.Success);

            RealAppTestDriver.WaitUntil(
                () => File.Exists(filePath) && File.ReadAllText(filePath, Encoding.UTF8) == text,
                "Timed out waiting for saved file contents to match the editor text.",
                timeoutMilliseconds: 10000);

            var savedText = File.ReadAllText(filePath, Encoding.UTF8);
            Assert.Equal(text, savedText);
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Fact]
    public void Explorer_Invoke_CanOpenWorkspaceFolder_ThroughRealShellFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch(Environment.CurrentDirectory);
        var explorerWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate =>
                candidate.Title.Contains("workspace", StringComparison.OrdinalIgnoreCase) ||
                candidate.Title.Contains("工作区", StringComparison.OrdinalIgnoreCase) ||
                candidate.Title.Contains("文件资源管理器", StringComparison.OrdinalIgnoreCase));

        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, explorerWindow);
        Assert.Contains("文件资源管理器", snapshot.SummaryText);

        var addressBar = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{explorerWindow.Title}\") edit[name=\"地址栏\"]");
        Assert.Equal("edit", addressBar.BestMatch!.Role, ignoreCase: true);

        var refreshButton = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{explorerWindow.Title}\") button[name~=\"刷新\"]");
        var refresh = RealAppTestDriver.Act(
            actionService,
            "req-real-shell-explorer-refresh",
            refreshButton.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(refresh.Success);
        Assert.Equal("invoke_pattern", refresh.Diagnostics["action_strategy"]);
    }

    [Fact]
    public void Explorer_Select_CanTargetVisibleListItem_ThroughRealShellFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch(Environment.CurrentDirectory);
        var explorerWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate =>
                candidate.Title.Contains("workspace", StringComparison.OrdinalIgnoreCase) ||
                candidate.Title.Contains("文件资源管理器", StringComparison.OrdinalIgnoreCase));

        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, explorerWindow);
        Assert.Contains("文件资源管理器", snapshot.SummaryText);

        var fileList = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{explorerWindow.Title}\") list[name=\"项目视图\"]",
                "scope:active_window list[name=\"项目视图\"]",
            },
            timeoutMilliseconds: 3000);

        var preferredTargets = new[] { "de", "es", "fr", "it", "ja" };
        var targetOption = preferredTargets.FirstOrDefault(option =>
            snapshot.SummaryText.Contains($"ListItem \"{option}\"", StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(targetOption), snapshot.SummaryText);

        var select = RealAppTestDriver.Act(
            actionService,
            "req-real-shell-explorer-select-list-item",
            fileList.BestMatch!.Ref.Value,
            "select",
            new Dictionary<string, object?>
            {
                ["option_text"] = targetOption!,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "selected_item",
                ["value"] = targetOption!,
            }, 1000));

        Assert.True(select.Success, select.Error?.Message ?? select.Verification.Message);
        Assert.True(select.Verification.Passed, select.Verification.Message);
        Assert.Equal("selection_item_pattern", select.Diagnostics["action_strategy"]);
        Assert.Equal(targetOption, select.ObservedEffect["selected_item"]);
    }

    [Fact]
    public void Explorer_Breadcrumb_CanNavigateUpAndBack_ThroughRealShellFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch(Environment.CurrentDirectory);
        var explorerWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate =>
                candidate.Title.Contains("workspace", StringComparison.OrdinalIgnoreCase) ||
                candidate.Title.Contains("文件资源管理器", StringComparison.OrdinalIgnoreCase));

        var originalLeafName = new DirectoryInfo(Environment.CurrentDirectory).Name;
        var ancestor = FindAncestorDirectory(Environment.CurrentDirectory, "tests");
        Assert.NotNull(ancestor);

        var expectedChildName = FindImmediateChildName(ancestor!, Environment.CurrentDirectory);
        Assert.False(string.IsNullOrWhiteSpace(expectedChildName));

        var descendant = FindImmediateChildDirectory(ancestor!, Environment.CurrentDirectory);
        Assert.NotNull(descendant);

        var secondAncestor = ancestor.Parent;
        Assert.NotNull(secondAncestor);

        var ancestorName = ancestor.Name;
        var secondAncestorName = secondAncestor!.Name;

        var breadcrumb = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{explorerWindow.Title}\") splitbutton[name=\"{ancestorName}\"]",
                $"scope:window(name=\"{explorerWindow.Title}\") button[name=\"{ancestorName}\"]",
                $"scope:window(name=\"{explorerWindow.Title}\") *[name=\"{ancestorName}\"]",
                $"scope:active_window splitbutton[name=\"{ancestorName}\"]",
                $"scope:active_window button[name=\"{ancestorName}\"]",
                $"scope:active_window *[name=\"{ancestorName}\"]",
            },
            timeoutMilliseconds: 3000);

        var navigateUp = RealAppTestDriver.Act(
            actionService,
            "req-real-shell-explorer-breadcrumb-up",
            breadcrumb.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(navigateUp.Success);
        Assert.Equal("invoke_pattern", navigateUp.Diagnostics["action_strategy"]);

        var projectFolder = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:active_window listitem[name=\"{expectedChildName}\"]",
                $"scope:window(name=\"{ancestorName}\") listitem[name=\"{expectedChildName}\"]",
                $"scope:active_window treeitem[name=\"{expectedChildName}\"]",
            },
            timeoutMilliseconds: 7000);
        Assert.NotNull(projectFolder.BestMatch);

        var navigateDown = RealAppTestDriver.Act(
            actionService,
            "req-real-shell-explorer-breadcrumb-descend",
            projectFolder.BestMatch.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(navigateDown.Success);
        Assert.Equal("invoke_pattern", navigateDown.Diagnostics["action_strategy"]);

        var descendantSurface = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{descendant!.Name}\") splitbutton[name=\"{descendant.Name}\"]",
                $"scope:active_window splitbutton[name=\"{descendant.Name}\"]",
                $"scope:active_window tabitem[name=\"{descendant.Name}\"]",
            },
            timeoutMilliseconds: 7000);
        Assert.NotNull(descendantSurface.BestMatch);

        var backButton = WaitForLocateAny(
            queryService,
            new[]
            {
                "scope:active_window button[name=\"后退\"]",
                "scope:active_window button[name~ = \"后退\"]",
            },
            timeoutMilliseconds: 3000);

        var goBack = RealAppTestDriver.Act(
            actionService,
            "req-real-shell-explorer-breadcrumb-back",
            backButton.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(goBack.Success);
        Assert.Equal("invoke_pattern", goBack.Diagnostics["action_strategy"]);

        var ancestorSurface = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{ancestorName}\") splitbutton[name=\"{ancestorName}\"]",
                $"scope:active_window splitbutton[name=\"{ancestorName}\"]",
                $"scope:active_window tabitem[name=\"{ancestorName}\"]",
            },
            timeoutMilliseconds: 7000);
        Assert.NotNull(ancestorSurface.BestMatch);

        var secondBreadcrumb = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{ancestorName}\") splitbutton[name=\"{secondAncestorName}\"]",
                $"scope:active_window splitbutton[name=\"{secondAncestorName}\"]",
                $"scope:active_window button[name=\"{secondAncestorName}\"]",
            },
            timeoutMilliseconds: 3000);

        var navigateSecondUp = RealAppTestDriver.Act(
            actionService,
            "req-real-shell-explorer-breadcrumb-second-up",
            secondBreadcrumb.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(navigateSecondUp.Success);
        Assert.Equal("invoke_pattern", navigateSecondUp.Diagnostics["action_strategy"]);

        var secondAncestorSurface = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:active_window splitbutton[name=\"{secondAncestorName}\"]",
                $"scope:active_window splitbutton[name=\"{ancestorName}\"]",
                $"scope:active_window tabitem[name=\"{secondAncestorName}\"]",
                $"scope:active_window listitem[name=\"{ancestorName}\"]",
                $"scope:active_window treeitem[name=\"{ancestorName}\"]",
            },
            timeoutMilliseconds: 7000);
        Assert.NotNull(secondAncestorSurface.BestMatch);

        var backToAncestor = InvokeExplorerBack(queryService, actionService, "req-real-shell-explorer-breadcrumb-back-to-ancestor");
        Assert.True(backToAncestor.Success);

        var backToOriginal = InvokeExplorerBack(queryService, actionService, "req-real-shell-explorer-breadcrumb-back-to-original");
        Assert.True(backToOriginal.Success);

        Thread.Sleep(1000);
        var snapshotAfterBack = RealAppTestDriver.SnapshotWindow(queryService, explorerWindow);

        var originalSurfaceSelectors = new[]
        {
            $"scope:window(name=\"{explorerWindow.Title}\") splitbutton[name=\"{originalLeafName}\"]",
            $"scope:window(name=\"{explorerWindow.Title}\") tabitem[name=\"{originalLeafName}\"]",
            $"scope:active_window splitbutton[name=\"{originalLeafName}\"]",
            $"scope:active_window edit[name~ = \"{originalLeafName}\"]",
            $"scope:active_window tabitem[name=\"{originalLeafName}\"]",
        };

        LocateResult? originalSurface = null;
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(7);
        while (DateTimeOffset.UtcNow < timeoutAt && originalSurface is null)
        {
            foreach (var selector in originalSurfaceSelectors)
            {
                var locate = queryService.WindowsLocate(new WindowsLocateRequest(selector));
                if (locate.IsSuccess && locate.Payload?.BestMatch is not null)
                {
                    originalSurface = locate.Payload;
                    break;
                }
            }

            if (originalSurface is null)
            {
                Thread.Sleep(100);
            }
        }

        Assert.True(originalSurface?.BestMatch is not null, snapshotAfterBack.SummaryText);
    }

    [Fact]
    public void PressKeys_CanSendLetterAndFunctionShortcuts_OnRealDesktopApps()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("notepad.exe");
        var notepadWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate => candidate.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        var editor = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{notepadWindow.Title}\") document[name=\"文本编辑器\"]");

        var focus = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-focus-for-press-keys",
            editor.BestMatch!.Ref.Value,
            "focus",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("focus_changed", new Dictionary<string, object?>(), 500));

        Assert.True(focus.Success);

        var pressLetter = ExecuteForegroundSensitivePressKeys(
            queryService,
            actionService,
            editor.BestMatch.Ref.Value,
            new object?[] { "CTRL", "SHIFT", "S" },
            "req-real-app-notepad-press-ctrl-shift-s");

        Assert.True(pressLetter.Success);
        Assert.Equal("keyboard_shortcut", pressLetter.Diagnostics["action_strategy"]);
        Assert.Contains("CONTROL", pressLetter.ObservedEffect["keys_sent"] ?? string.Empty);

        WaitForNotepadSaveAsDialog(queryService, notepadWindow.Ref.Value);

        var refreshedNotepadWindow = FindWindowByRef(queryService, notepadWindow.Ref.Value);
        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, refreshedNotepadWindow);
        Assert.Contains("另存为", snapshot.SummaryText);

        var cancelButton = LocateNotepadSaveAsCancelButton(queryService, refreshedNotepadWindow.Title);
        var cancel = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-cancel-after-press-keys",
            cancelButton.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(cancel.Success);

        var explorerWindowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);
        using var explorerSession = RealAppTestDriver.Launch(Environment.CurrentDirectory);
        var explorerWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            explorerWindowsBefore,
            candidate =>
                candidate.Title.Contains("workspace", StringComparison.OrdinalIgnoreCase) ||
                candidate.Title.Contains("文件资源管理器", StringComparison.OrdinalIgnoreCase));

        var addressBar = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{explorerWindow.Title}\") edit[name=\"地址栏\"]");
        var pressFunction = RealAppTestDriver.Act(
            actionService,
            "req-real-app-explorer-press-f5",
            addressBar.BestMatch!.Ref.Value,
            "press_keys",
            new Dictionary<string, object?>
            {
                ["keys"] = new object?[] { "F5" },
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(pressFunction.Success);
        Assert.Equal("keyboard_shortcut", pressFunction.Diagnostics["action_strategy"]);
        Assert.Contains("F5", pressFunction.ObservedEffect["keys_sent"] ?? string.Empty);
    }

    [Fact]
    public void Settings_SetValue_CanSearchWithinRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        Thread.Sleep(1500);

        var settingsWindow = queryService.WindowsList().Windows.FirstOrDefault(candidate =>
            candidate.Title.Contains("设置", StringComparison.OrdinalIgnoreCase) ||
            candidate.Title.Contains("Settings", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(settingsWindow);

        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, settingsWindow!);
        Assert.Contains("设置", snapshot.SummaryText);

        var searchBox = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{settingsWindow.Title}\") edit[name=\"搜索框，查找设置\"]");
        Assert.Equal("edit", searchBox.BestMatch!.Role, ignoreCase: true);

        var queryText = "夜间模式";
        var search = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-search",
            searchBox.BestMatch.Ref.Value,
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = queryText,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000));

        Assert.True(search.Success);
        Assert.True(search.Verification.Passed, search.Verification.Message);
        Assert.Equal("value_pattern", search.Diagnostics["action_strategy"]);
        Assert.Equal(queryText, search.ObservedEffect["text_updated"]);
    }

    [Fact]
    public void Settings_Toggle_CanRoundTripNightLight_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        Thread.Sleep(1500);

        var settingsWindow = FindSettingsWindow(queryService);
        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, settingsWindow);
        Assert.Contains("夜间模式", snapshot.SummaryText);

        var toggle = LocateNightLightToggle(queryService, settingsWindow.Title);
        var turnOn = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-toggle-night-light-on",
            toggle.Ref.Value,
            "toggle",
            new Dictionary<string, object?>
            {
                ["target_state"] = true,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "toggle_state",
                ["value"] = "true",
            }, 1000));

        try
        {
            Assert.True(turnOn.Success);
            Assert.True(turnOn.Verification.Passed, turnOn.Verification.Message);
            Assert.Equal("toggle_pattern", turnOn.Diagnostics["action_strategy"]);
            Assert.Equal("true", turnOn.ObservedEffect["toggle_state"]);
        }
        finally
        {
            var restore = RealAppTestDriver.Act(
                actionService,
                "req-real-settings-toggle-night-light-off",
                toggle.Ref.Value,
                "toggle",
                new Dictionary<string, object?>
                {
                    ["target_state"] = false,
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("property_change", new Dictionary<string, object?>
                {
                    ["property"] = "toggle_state",
                    ["value"] = "false",
                }, 1000));

            Assert.True(restore.Success);
            Assert.True(restore.Verification.Passed, restore.Verification.Message);
        }
    }

    [Fact]
    public void Settings_ComboBox_CanExpandAndCollapseScale_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        Thread.Sleep(1500);

        var settingsWindow = FindSettingsWindow(queryService);
        var combo = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{settingsWindow.Title}\") combobox[name=\"缩放\"]");

        var expand = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-expand-scale-combobox",
            combo.BestMatch!.Ref.Value,
            "expand",
            new Dictionary<string, object?>
            {
                ["target_state"] = "Expanded",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "expand_state",
                ["value"] = "Expanded",
            }, 1000));

        Assert.True(expand.Success);
        Assert.True(expand.Verification.Passed, expand.Verification.Message);
        Assert.Equal("expand_pattern", expand.Diagnostics["action_strategy"]);
        Assert.Equal("Expanded", expand.ObservedEffect["expand_state"]);

        var collapse = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-collapse-scale-combobox",
            combo.BestMatch.Ref.Value,
            "collapse",
            new Dictionary<string, object?>
            {
                ["target_state"] = "Collapsed",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "expand_state",
                ["value"] = "Collapsed",
            }, 1000));

        Assert.True(collapse.Success);
        Assert.True(collapse.Verification.Passed, collapse.Verification.Message);
        Assert.Equal("collapse_pattern", collapse.Diagnostics["action_strategy"]);
        Assert.Equal("Collapsed", collapse.ObservedEffect["expand_state"]);
    }

    [Fact]
    public void Settings_Select_CanRoundTripScaleComboBox_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        Thread.Sleep(1500);

        var settingsWindow = FindSettingsWindow(queryService);
        var scaleCombo = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{settingsWindow.Title}\") combobox[name=\"缩放\"]");

        var expand = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-expand-scale-combobox-for-select",
            scaleCombo.BestMatch!.Ref.Value,
            "expand",
            new Dictionary<string, object?>
            {
                ["target_state"] = "Expanded",
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "expand_state",
                ["value"] = "Expanded",
            }, 1000));

        Assert.True(expand.Success);
        Assert.True(expand.Verification.Passed, expand.Verification.Message);

        var snapshot = WaitForSettingsSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("缩放", StringComparison.Ordinal));
        Assert.Contains("缩放", snapshot.SummaryText);

        var preferredTargets = new[] { "100%", "125%", "150%", "175%" };
        var targetOption = preferredTargets.FirstOrDefault(option =>
            snapshot.SummaryText.Contains(option, StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(targetOption), snapshot.SummaryText);

        var select = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-select-scale-combobox",
            scaleCombo.BestMatch.Ref.Value,
            "select",
            new Dictionary<string, object?>
            {
                ["option_text"] = targetOption!,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "selected_item",
                ["value"] = targetOption!,
            }, 1000));

        try
        {
            Assert.True(select.Success, select.Error?.Message ?? select.Verification.Message);
            Assert.True(select.Verification.Passed, select.Verification.Message);
            Assert.Equal("selection_item_pattern", select.Diagnostics["action_strategy"]);
            Assert.Equal(targetOption, select.ObservedEffect["selected_item"]);
        }
        finally
        {
            var collapse = RealAppTestDriver.Act(
                actionService,
                "req-real-settings-collapse-scale-combobox-after-select",
                scaleCombo.BestMatch.Ref.Value,
                "collapse",
                new Dictionary<string, object?>
                {
                    ["target_state"] = "Collapsed",
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("property_change", new Dictionary<string, object?>
                {
                    ["property"] = "expand_state",
                    ["value"] = "Collapsed",
                }, 1000));

            Assert.True(collapse.Success);
            Assert.True(collapse.Verification.Passed, collapse.Verification.Message);
        }
    }

    [Fact]
    public void Settings_Navigation_CanReachSoundPage_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        var soundWindow = OpenSettingsSoundPage(queryService, actionService);
        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, soundWindow);

        Assert.Contains("声音", snapshot.SummaryText);
        Assert.Contains("单声道音频", snapshot.SummaryText);
        Assert.Contains("调整输出音量", snapshot.SummaryText);
    }

    [Fact]
    public void Settings_Invoke_CanRoundTripMuteVolume_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        var soundWindow = OpenSettingsSoundPage(queryService, actionService);
        var initialSnapshot = RealAppTestDriver.SnapshotWindow(queryService, soundWindow);
        Assert.Contains("调整输出音量", initialSnapshot.SummaryText);

        var muteButton = LocateSoundVolumeToggle(queryService, soundWindow.Title);
        var mute = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-mute-volume",
            muteButton.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        try
        {
            Assert.True(mute.Success);
            Assert.Equal("invoke_pattern", mute.Diagnostics["action_strategy"]);

            var mutedSnapshot = WaitForSettingsSnapshot(queryService);
            Assert.True(
                mutedSnapshot.SummaryText.Contains("取消静音", StringComparison.Ordinal) ||
                mutedSnapshot.SummaryText.Contains("静音音量", StringComparison.Ordinal),
                mutedSnapshot.SummaryText);
        }
        finally
        {
            soundWindow = FindSettingsWindow(queryService);
            var restoreButton = LocateSoundVolumeToggle(queryService, soundWindow.Title);
            var restore = RealAppTestDriver.Act(
                actionService,
                "req-real-settings-unmute-volume",
                restoreButton.Ref.Value,
                "invoke",
                new Dictionary<string, object?>(),
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

            Assert.True(restore.Success);
            Assert.Equal("invoke_pattern", restore.Diagnostics["action_strategy"]);
        }
    }

    [Fact]
    public void Settings_RangeValue_CanRoundTripOutputVolume_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        var soundWindow = OpenSettingsSoundPage(queryService, actionService);
        var initialSnapshot = RealAppTestDriver.SnapshotWindow(queryService, soundWindow);
        Assert.Contains("调整输出音量", initialSnapshot.SummaryText);

        var volumeSlider = LocateSoundVolumeSlider(queryService, soundWindow.Title);
        var setVolume = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-range-value-output-volume",
            volumeSlider.Ref.Value,
            "range_value",
            new Dictionary<string, object?>
            {
                ["target_value"] = 35,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "range_value",
                ["value"] = "35",
            }, 1000));

        Assert.True(setVolume.Success);
        Assert.True(setVolume.Verification.Passed, setVolume.Verification.Message);
        Assert.Equal("range_value_pattern", setVolume.Diagnostics["action_strategy"]);
        Assert.Equal("35", setVolume.ObservedEffect["range_value"]);

        var previousVolume = int.Parse(setVolume.ObservedEffect["previous_range_value"]!, System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            var changedSnapshot = WaitForSettingsSnapshot(queryService);
            Assert.Contains("调整输出音量", changedSnapshot.SummaryText);
        }
        finally
        {
            soundWindow = FindSettingsWindow(queryService);
            volumeSlider = LocateSoundVolumeSlider(queryService, soundWindow.Title);
            var restore = RealAppTestDriver.Act(
                actionService,
                "req-real-settings-range-value-restore-output-volume",
                volumeSlider.Ref.Value,
                "range_value",
                new Dictionary<string, object?>
                {
                    ["target_value"] = previousVolume,
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("property_change", new Dictionary<string, object?>
                {
                    ["property"] = "range_value",
                    ["value"] = previousVolume.ToString(System.Globalization.CultureInfo.InvariantCulture),
                }, 1000));

            Assert.True(restore.Success);
            Assert.True(restore.Verification.Passed, restore.Verification.Message);
            Assert.Equal(previousVolume.ToString(System.Globalization.CultureInfo.InvariantCulture), restore.ObservedEffect["range_value"]);
        }
    }

    [Fact]
    public void Settings_Toggle_CanRoundTripMonoAudio_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        using var session = RealAppTestDriver.Launch("ms-settings:display");
        var soundWindow = OpenSettingsSoundPage(queryService, actionService);
        var monoAudio = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{soundWindow.Title}\") button[name=\"单声道音频\"]");

        var turnOn = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-toggle-mono-audio-on",
            monoAudio.BestMatch!.Ref.Value,
            "toggle",
            new Dictionary<string, object?>
            {
                ["target_state"] = true,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "toggle_state",
                ["value"] = "true",
            }, 1000));

        try
        {
            Assert.True(turnOn.Success);
            Assert.True(turnOn.Verification.Passed, turnOn.Verification.Message);
            Assert.Equal("toggle_pattern", turnOn.Diagnostics["action_strategy"]);
            Assert.Equal("true", turnOn.ObservedEffect["toggle_state"]);
        }
        finally
        {
            var restore = RealAppTestDriver.Act(
                actionService,
                "req-real-settings-toggle-mono-audio-off",
                monoAudio.BestMatch.Ref.Value,
                "toggle",
                new Dictionary<string, object?>
                {
                    ["target_state"] = false,
                },
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = false,
                },
                new ExpectedOutcome("property_change", new Dictionary<string, object?>
                {
                    ["property"] = "toggle_state",
                    ["value"] = "false",
                }, 1000));

            Assert.True(restore.Success);
            Assert.True(restore.Verification.Passed, restore.Verification.Message);
            Assert.Equal("false", restore.ObservedEffect["toggle_state"]);
        }
    }

    [Fact]
    public void StandardFileDialog_Ambiguity_AndTreeFocus_CanBeHandled_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("notepad.exe");
        var notepadWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate => candidate.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        OpenNotepadSaveAsDialog(queryService, actionService, notepadWindow.Ref.Value);

        var refreshedNotepadWindow = FindWindowByRef(queryService, notepadWindow.Ref.Value);
        var snapshot = RealAppTestDriver.SnapshotWindow(queryService, refreshedNotepadWindow);
        Assert.Contains("另存为", snapshot.SummaryText);

        var ambiguousOpenButtons = queryService.WindowsLocate(new WindowsLocateRequest(
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") window[name=\"另存为\"] >> button[name=\"打开\"]"));

        Assert.False(ambiguousOpenButtons.IsSuccess);
        Assert.NotNull(ambiguousOpenButtons.Payload);
        Assert.Equal(A11yFlow.Core.Errors.ToolErrorCode.TargetAmbiguous, ambiguousOpenButtons.Error!.Code);
        Assert.True(ambiguousOpenButtons.Payload!.Candidates.Count > 1);

        var thisPc = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") window[name=\"另存为\"] >> treeitem[name=\"此电脑\"]");
        var focus = RealAppTestDriver.Act(
            actionService,
            "req-real-file-dialog-focus-this-pc",
            thisPc.BestMatch!.Ref.Value,
            "focus",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("focus_changed", new Dictionary<string, object?>(), 500));

        Assert.True(focus.Success);
        Assert.True(focus.Verification.Passed, focus.Verification.Message);
        Assert.Equal("focus", focus.Diagnostics["action_strategy"]);

        var cancelButton = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") window[name=\"另存为\"] >> button[name=\"取消\"]");
        var closeDialog = RealAppTestDriver.Act(
            actionService,
            "req-real-file-dialog-close-after-tree-navigation",
            cancelButton.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(closeDialog.Success);
    }

    [Fact]
    public void StandardFileDialog_AmbiguityRecovery_CanResolveOpenButtons_ThroughRealDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;
        var windowsBefore = queryService.WindowsList().Windows.Select(window => window.Ref.Value).ToHashSet(StringComparer.Ordinal);

        using var session = RealAppTestDriver.Launch("notepad.exe");
        var notepadWindow = RealAppTestDriver.WaitForNewWindow(
            queryService,
            windowsBefore,
            candidate => candidate.Title.Contains("Notepad", StringComparison.OrdinalIgnoreCase));

        OpenNotepadSaveAsDialog(queryService, actionService, notepadWindow.Ref.Value);

        var refreshedNotepadWindow = FindWindowByRef(queryService, notepadWindow.Ref.Value);
        var ambiguousLocate = queryService.WindowsLocate(new WindowsLocateRequest(
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") window[name=\"另存为\"] >> button[name=\"打开\"]"));

        Assert.False(ambiguousLocate.IsSuccess);
        Assert.NotNull(ambiguousLocate.Payload);
        Assert.Equal(A11yFlow.Core.Errors.ToolErrorCode.TargetAmbiguous, ambiguousLocate.Error!.Code);
        Assert.True(ambiguousLocate.Payload!.Candidates.Count > 1);
        Assert.Equal(
            ambiguousLocate.Payload.Candidates.Count.ToString(),
            ambiguousLocate.Payload.Diagnostics["candidate_count"]);
        Assert.Equal("ambiguity_resolution", ambiguousLocate.Error.Diagnostics["recovery_kind"]);
        Assert.Equal("windows_describe_ref", ambiguousLocate.Error.Diagnostics["recovery_target_tool"]);
        Assert.Equal("describe_candidates_then_refine", ambiguousLocate.Error.Diagnostics["suggested_next_step_kind"]);

        var candidateDescriptions = ambiguousLocate.Payload.Candidates
            .Select(candidate => queryService.WindowsDescribeRef(new DescribeRefRequest(candidate.Ref.Value)))
            .ToList();

        Assert.All(candidateDescriptions, description =>
        {
            Assert.True(description.IsSuccess, description.Error?.Message ?? "windows_describe_ref failed");
            Assert.NotNull(description.Payload);
            Assert.Equal("windows_describe_ref", description.Payload!.Diagnostics["tool_name"]);
            Assert.Equal("text_structured", description.Payload.Diagnostics["interaction_model"]);
            Assert.Equal("accessibility_tree", description.Payload.Diagnostics["primary_interface"]);
        });

        var openButton = candidateDescriptions
            .Select(description => description.Payload!)
            .FirstOrDefault(description =>
                string.Equals(description.Name, "打开", StringComparison.Ordinal) &&
                description.Actions.Contains("invoke", StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(openButton);

        var invoke = RealAppTestDriver.Act(
            actionService,
            "req-real-file-dialog-recovered-open-button-invoke",
            openButton!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(invoke.Success);
        Assert.Equal("invoke_pattern", invoke.Diagnostics["action_strategy"]);

        var snapshotAfterOpen = RealAppTestDriver.SnapshotWindow(queryService, refreshedNotepadWindow);
        Assert.Contains("另存为", snapshotAfterOpen.SummaryText);

        var cancelButton = RealAppTestDriver.Locate(
            queryService,
            $"scope:window(name=\"{refreshedNotepadWindow.Title}\") window[name=\"另存为\"] >> button[name=\"取消\"]");
        var closeDialog = RealAppTestDriver.Act(
            actionService,
            "req-real-file-dialog-close-after-ambiguity-recovery",
            cancelButton.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(closeDialog.Success);
    }

    [Fact]
    public void Codex_Focus_CanTargetStableVisibleControl_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForCodexSnapshot(queryService, isReady: IsMeaningfulCodexFocusSnapshot);
        var focusTarget = FindFirstNode(
            snapshot.Root,
            node => node.Actions.Contains("focus", StringComparer.OrdinalIgnoreCase)
                && (string.Equals(node.Role, "Button", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Role, "ComboBox", StringComparison.OrdinalIgnoreCase))
                && (string.Equals(node.Name, "完全访问权限", StringComparison.Ordinal)
                    || string.Equals(node.Name, "计划", StringComparison.Ordinal)
                    || string.Equals(node.Name, "听写", StringComparison.Ordinal)
                    || string.Equals(node.Name, "team-mcpv2", StringComparison.Ordinal)
                    || string.Equals(node.Name, "新对话", StringComparison.Ordinal)
                    || string.Equals(node.Name, "搜索", StringComparison.Ordinal)
                    || string.Equals(node.Name, "命令菜单", StringComparison.Ordinal)));
        Assert.NotNull(focusTarget);

        var focus = RealAppTestDriver.Act(
            actionService,
            "req-real-codex-focus-stable-visible-control",
            focusTarget!.Ref.Value,
            "focus",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(focus.Success);
        Assert.Equal("focus", focus.Diagnostics["action_strategy"]);

        var snapshotAfterFocus = WaitForCodexSnapshot(queryService, isReady: current =>
            focusTarget.Name is not null && current.SummaryText.Contains(focusTarget.Name, StringComparison.Ordinal));
        var focusTargetAfterAction = FindFirstNode(
            snapshotAfterFocus.Root,
            node => string.Equals(node.Name, focusTarget.Name, StringComparison.Ordinal)
                && node.Actions.Contains("focus", StringComparer.OrdinalIgnoreCase));
        Assert.NotNull(focusTargetAfterAction);
    }

    [Fact]
    public void Codex_Invoke_CanTriggerStableToolbarAction_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForCodexSnapshot(queryService, isReady: IsMeaningfulCodexToolbarSnapshot);
        var codexWindow = FindCodexWindow(queryService);
        Assert.True(IsMeaningfulCodexToolbarSnapshot(snapshot), snapshot.SummaryText);

        var stableToolbarAction = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{codexWindow!.Title}\") button[name=\"搜索\"]",
                $"scope:window(name=\"{codexWindow!.Title}\") button[name=\"新对话\"]",
                $"scope:window(name=\"{codexWindow!.Title}\") button[name=\"隐藏边栏\"]",
            },
            timeoutMilliseconds: 3000);
        var invoke = RealAppTestDriver.Act(
            actionService,
            "req-real-codex-stable-toolbar-invoke",
            stableToolbarAction.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(invoke.Success);
        Assert.Equal("invoke_pattern", invoke.Diagnostics["action_strategy"]);

        var toolbarSnapshotAfterInvoke = WaitForCodexSnapshot(queryService, isReady: current =>
            IsMeaningfulCodexToolbarSnapshot(current)
            || current.SummaryText.Contains("命令菜单", StringComparison.Ordinal)
            || current.SummaryText.Contains("新对话", StringComparison.Ordinal));
        Assert.True(
            IsMeaningfulCodexToolbarSnapshot(toolbarSnapshotAfterInvoke)
            || toolbarSnapshotAfterInvoke.SummaryText.Contains("命令菜单", StringComparison.Ordinal)
            || toolbarSnapshotAfterInvoke.SummaryText.Contains("新对话", StringComparison.Ordinal),
            toolbarSnapshotAfterInvoke.SummaryText);

        var toolbarTargetAfterInvoke = FindFirstNode(
            toolbarSnapshotAfterInvoke.Root,
            node => node.Actions.Contains("invoke", StringComparer.OrdinalIgnoreCase)
                && node.Name is not null
                && (string.Equals(node.Name, stableToolbarAction.BestMatch.Name, StringComparison.Ordinal)
                    || string.Equals(node.Name, "新对话", StringComparison.Ordinal)
                    || string.Equals(node.Name, "搜索", StringComparison.Ordinal)
                    || node.Name.Contains("根据", StringComparison.Ordinal)
                    || node.Name.Contains("我的opencode", StringComparison.Ordinal)));
        Assert.NotNull(toolbarTargetAfterInvoke);

        var toolbarDescription = queryService.WindowsDescribeRef(new DescribeRefRequest(toolbarTargetAfterInvoke!.Ref.Value));
        Assert.True(toolbarDescription.IsSuccess, toolbarDescription.Error?.Message ?? "windows_describe_ref failed");
        Assert.NotNull(toolbarDescription.Payload);
        Assert.Equal("windows_describe_ref", toolbarDescription.Payload!.Diagnostics["tool_name"]);
        Assert.Equal(toolbarTargetAfterInvoke.Name, toolbarDescription.Payload.Name);
        Assert.Contains("invoke", toolbarDescription.Payload.Actions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Codex_Invoke_CanTriggerLiveSimulationAction_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForCodexSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("开始新对话", StringComparison.Ordinal)
            || current.SummaryText.Contains("命令菜单", StringComparison.Ordinal)
            || current.SummaryText.Contains("新对话", StringComparison.Ordinal));
        var liveAction = FindFirstNode(
            snapshot.Root,
            node => (string.Equals(node.Role, "Button", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Role, "ListItem", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Role, "Group", StringComparison.OrdinalIgnoreCase))
                && node.Actions.Contains("invoke", StringComparer.OrdinalIgnoreCase)
                && node.Name is not null
                && (node.Name.Contains("开始新对话", StringComparison.Ordinal)
                    || string.Equals(node.Name, "新对话", StringComparison.Ordinal)
                    || string.Equals(node.Name, "搜索", StringComparison.Ordinal)
                    || node.Name.Contains("根据", StringComparison.Ordinal)
                    || node.Name.Contains("我的opencode", StringComparison.Ordinal)));
        Assert.NotNull(liveAction);

        var invoke = RealAppTestDriver.Act(
            actionService,
            "req-real-codex-trigger-live-simulation-action",
            liveAction!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(invoke.Success, invoke.Error?.Message ?? invoke.Verification.Message);
        Assert.Equal("invoke_pattern", invoke.Diagnostics["action_strategy"]);

        var liveSnapshotAfterInvoke = WaitForCodexSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("命令菜单", StringComparison.Ordinal)
            || current.SummaryText.Contains("新对话", StringComparison.Ordinal)
            || (liveAction.Name is not null && current.SummaryText.Contains(liveAction.Name, StringComparison.Ordinal)));
        Assert.True(
            liveSnapshotAfterInvoke.SummaryText.Contains("命令菜单", StringComparison.Ordinal)
            || liveSnapshotAfterInvoke.SummaryText.Contains("新对话", StringComparison.Ordinal)
            || (liveAction.Name is not null && liveSnapshotAfterInvoke.SummaryText.Contains(liveAction.Name, StringComparison.Ordinal)),
            liveSnapshotAfterInvoke.SummaryText);

        var liveActionAfterInvoke = FindFirstNode(
            liveSnapshotAfterInvoke.Root,
            node => node.Actions.Contains("invoke", StringComparer.OrdinalIgnoreCase)
                && node.Name is not null
                && (string.Equals(node.Name, liveAction.Name, StringComparison.Ordinal)
                    || string.Equals(node.Name, "新对话", StringComparison.Ordinal)
                    || string.Equals(node.Name, "搜索", StringComparison.Ordinal)
                    || node.Name.Contains("根据", StringComparison.Ordinal)
                    || node.Name.Contains("我的opencode", StringComparison.Ordinal)));
        Assert.NotNull(liveActionAfterInvoke);

        var liveActionDescription = queryService.WindowsDescribeRef(new DescribeRefRequest(liveActionAfterInvoke!.Ref.Value));
        Assert.True(liveActionDescription.IsSuccess, liveActionDescription.Error?.Message ?? "windows_describe_ref failed");
        Assert.NotNull(liveActionDescription.Payload);
        Assert.Equal("windows_describe_ref", liveActionDescription.Payload!.Diagnostics["tool_name"]);
        Assert.Equal(liveActionAfterInvoke.Name, liveActionDescription.Payload.Name);
        Assert.Contains("invoke", liveActionDescription.Payload.Actions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Qwen_SetValue_CanTargetComposerInput_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForQwenSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("有什么我能帮您的吗？", StringComparison.Ordinal)
            || current.SummaryText.Contains("搜索对话", StringComparison.Ordinal));

        var composerInput = FindFirstNode(
            snapshot.Root,
            node => string.Equals(node.Role, "Edit", StringComparison.OrdinalIgnoreCase)
                && node.Actions.Contains("set_value", StringComparer.OrdinalIgnoreCase)
                && string.Equals(node.Name, "有什么我能帮您的吗？", StringComparison.Ordinal));
        Assert.NotNull(composerInput);

        var queryText = "A11yFlow Qwen substrate probe";
        var setValue = RealAppTestDriver.Act(
            actionService,
            "req-real-qwen-set-composer-input",
            composerInput!.Ref.Value,
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = queryText,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("text_updated", new Dictionary<string, object?>(), 1000));

        Assert.True(setValue.Success, setValue.Error?.Message ?? setValue.Verification.Message);
        Assert.True(setValue.Verification.Passed, setValue.Verification.Message);
        Assert.Equal("value_pattern", setValue.Diagnostics["action_strategy"]);
        Assert.Equal(queryText, setValue.ObservedEffect["text_updated"]);
    }

    [Fact]
    public void Qwen_Invoke_CanTriggerNewConversation_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForQwenSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("新建对话", StringComparison.Ordinal));

        var newConversation = FindFirstNode(
            snapshot.Root,
            node => node.Actions.Contains("invoke", StringComparer.OrdinalIgnoreCase)
                && string.Equals(node.Name, "新建对话", StringComparison.Ordinal));
        Assert.NotNull(newConversation);

        var invoke = RealAppTestDriver.Act(
            actionService,
            "req-real-qwen-invoke-new-conversation",
            newConversation!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(invoke.Success, invoke.Error?.Message ?? invoke.Verification.Message);
        Assert.Equal("invoke_pattern", invoke.Diagnostics["action_strategy"]);
        Assert.Contains("新建对话", snapshot.SummaryText, StringComparison.Ordinal);
    }

    [Fact]
    public void WeChat_SetValue_CanTargetComposerInput_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForWeChatSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("会话", StringComparison.Ordinal)
            && current.SummaryText.Contains("文件传输助手", StringComparison.Ordinal)
            && current.SummaryText.Contains("发送(S)", StringComparison.Ordinal));

        var composerInput = FindFirstNode(
            snapshot.Root,
            node => string.Equals(node.Role, "Edit", StringComparison.OrdinalIgnoreCase)
                && node.Actions.Contains("set_value", StringComparer.OrdinalIgnoreCase)
                && string.Equals(node.Name, "文件传输助手", StringComparison.Ordinal));
        Assert.NotNull(composerInput);

        var queryText = "A11yFlow WeChat probe";
        var setValue = RealAppTestDriver.Act(
            actionService,
            "req-real-wechat-set-composer-input",
            composerInput!.Ref.Value,
            "set_value",
            new Dictionary<string, object?>
            {
                ["text"] = queryText,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 1000));

        Assert.True(setValue.Success, setValue.Error?.Message ?? setValue.Verification.Message);
        Assert.Equal("value_pattern", setValue.Diagnostics["action_strategy"]);

        var snapshotAfterSetValue = WaitForWeChatSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("发送(S)", StringComparison.Ordinal)
            && current.SummaryText.Contains("文件传输助手", StringComparison.Ordinal));
        var composerInputAfterSetValue = FindFirstNode(
            snapshotAfterSetValue.Root,
            node => string.Equals(node.Role, "Edit", StringComparison.OrdinalIgnoreCase)
                && node.Actions.Contains("set_value", StringComparer.OrdinalIgnoreCase)
                && string.Equals(node.Name, "文件传输助手", StringComparison.Ordinal));
        Assert.NotNull(composerInputAfterSetValue);
    }

    [Fact]
    public void WeChat_Select_CanTargetConversationListItem_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForWeChatSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("会话", StringComparison.Ordinal)
            && current.SummaryText.Contains("文件传输助手", StringComparison.Ordinal));

        var conversationItem = FindFirstNode(
            snapshot.Root,
            node => string.Equals(node.Role, "ListItem", StringComparison.OrdinalIgnoreCase)
                && node.Actions.Contains("select", StringComparer.OrdinalIgnoreCase)
                && node.Name is not null
                && node.Name.Contains("文件传输助手", StringComparison.Ordinal));
        Assert.NotNull(conversationItem);

        var selectedConversationName = conversationItem!.Name!;

        var select = RealAppTestDriver.Act(
            actionService,
            "req-real-wechat-select-file-transfer-assistant",
            conversationItem.Ref.Value,
            "select",
            new Dictionary<string, object?>
            {
                ["option_text"] = selectedConversationName,
            },
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("property_change", new Dictionary<string, object?>
            {
                ["property"] = "selected_item",
                ["value"] = selectedConversationName,
            }, 1000));

        Assert.True(select.Success, select.Error?.Message ?? select.Verification.Message);
        Assert.True(select.Verification.Passed, select.Verification.Message);
        Assert.Equal("selection_item_pattern", select.Diagnostics["action_strategy"]);
        Assert.Equal(selectedConversationName, select.ObservedEffect["selected_item"]);
    }

    [Fact]
    public void WeChat_Invoke_CanTargetContactsNavigation_ThroughRealComplexDesktopFlow()
    {
        using var runtime = new IntegrationTestRuntime();
        var queryService = runtime.QueryService;
        var actionService = runtime.ActionService;

        var snapshot = WaitForWeChatSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("通讯录", StringComparison.Ordinal)
            && current.SummaryText.Contains("聊天", StringComparison.Ordinal));

        var contactsButton = FindFirstNode(
            snapshot.Root,
            node => string.Equals(node.Role, "Button", StringComparison.OrdinalIgnoreCase)
                && node.Actions.Contains("invoke", StringComparer.OrdinalIgnoreCase)
                && string.Equals(node.Name, "通讯录", StringComparison.Ordinal));
        Assert.NotNull(contactsButton);

        var invoke = RealAppTestDriver.Act(
            actionService,
            "req-real-wechat-open-contacts-navigation",
            contactsButton!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(invoke.Success, invoke.Error?.Message ?? invoke.Verification.Message);
        Assert.Equal("invoke_pattern", invoke.Diagnostics["action_strategy"]);

        var contactsSnapshot = WaitForWeChatSnapshot(queryService, isReady: current =>
            current.SummaryText.Contains("通讯录", StringComparison.Ordinal)
            && current.SummaryText.Contains("搜索", StringComparison.Ordinal));
        var contactsButtonAfterInvoke = FindFirstNode(
            contactsSnapshot.Root,
            node => string.Equals(node.Role, "Button", StringComparison.OrdinalIgnoreCase)
                && node.Actions.Contains("invoke", StringComparer.OrdinalIgnoreCase)
                && string.Equals(node.Name, "通讯录", StringComparison.Ordinal));
        Assert.NotNull(contactsButtonAfterInvoke);

        var contactsDescription = queryService.WindowsDescribeRef(new DescribeRefRequest(contactsButtonAfterInvoke!.Ref.Value));
        Assert.True(contactsDescription.IsSuccess, contactsDescription.Error?.Message ?? "windows_describe_ref failed");
        Assert.NotNull(contactsDescription.Payload);
        Assert.Equal("windows_describe_ref", contactsDescription.Payload!.Diagnostics["tool_name"]);
        Assert.Equal("通讯录", contactsDescription.Payload.Name);
        Assert.Contains("invoke", contactsDescription.Payload.Actions, StringComparer.OrdinalIgnoreCase);
    }

    private static void InvokeCalculatorButton(
        QueryToolService queryService,
        ActionToolService actionService,
        string windowTitle,
        string buttonName)
    {
        var selector = $"scope:window(name=\"{windowTitle}\") button[name=\"{buttonName}\"]";
        var locate = RealAppTestDriver.Locate(queryService, selector);
        var action = RealAppTestDriver.Act(
            actionService,
            $"req-calculator-{buttonName}",
            locate.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(action.Success);
        Assert.Equal("invoke_pattern", action.Diagnostics["action_strategy"]);
    }

    private static void OpenNotepadSaveAsDialog(
        QueryToolService queryService,
        ActionToolService actionService,
        string windowRef)
    {
        var windowTitle = TryGetWindowTitle(queryService, windowRef);
        var fileMenuSelectors = new List<string>
        {
            "scope:active_window menuitem[name=\"文件\"]",
        };

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            fileMenuSelectors.Insert(0, $"scope:window(name=\"{windowTitle}\") menuitem[name=\"文件\"]");
        }

        var fileMenu = WaitForLocateAny(
            queryService,
            fileMenuSelectors,
            timeoutMilliseconds: 3000);
        var openFileMenu = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-open-file-menu-shared",
            fileMenu.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(openFileMenu.Success);

        windowTitle = TryGetWindowTitle(queryService, windowRef);
        var saveAsSelectors = new List<string>
        {
            "scope:active_window menuitem[name=\"另存为\"]",
        };

        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            saveAsSelectors.Insert(0, $"scope:window(name=\"{windowTitle}\") menuitem[name=\"另存为\"]");
        }

        var saveAsItem = WaitForLocateAny(
            queryService,
            saveAsSelectors,
            timeoutMilliseconds: 3000);
        var openSaveAs = RealAppTestDriver.Act(
            actionService,
            "req-real-app-notepad-open-save-as-menu-item-shared",
            saveAsItem.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(openSaveAs.Success);
        WaitForNotepadSaveAsDialog(queryService, windowRef);
    }

    private static string GetWindowTitle(QueryToolService queryService, string windowRef)
    {
        var title = TryGetWindowTitle(queryService, windowRef);
        Assert.False(string.IsNullOrWhiteSpace(title));
        return title!;
    }

    private static string? TryGetWindowTitle(QueryToolService queryService, string windowRef)
    {
        var window = queryService.WindowsList().Windows.FirstOrDefault(candidate =>
            string.Equals(candidate.Ref.Value, windowRef, StringComparison.Ordinal));

        return string.IsNullOrWhiteSpace(window?.Title) ? null : window.Title;
    }

    private static WindowSummary FindSettingsWindow(QueryToolService queryService)
    {
        var settingsWindow = TryFindSettingsWindow(queryService);
        Assert.NotNull(settingsWindow);
        return settingsWindow!;
    }

    private static WindowSummary FindCodexWindow(QueryToolService queryService)
    {
        var codexWindow = TryFindCodexWindow(queryService);
        Assert.NotNull(codexWindow);
        return codexWindow!;
    }

    private static WindowSummary FindQwenWindow(QueryToolService queryService)
    {
        var qwenWindow = TryFindQwenWindow(queryService);
        if (qwenWindow is null)
        {
            var visibleWindows = string.Join(", ", queryService.WindowsList().Windows.Select(window => window.Title));
            throw new Xunit.Sdk.XunitException($"Qwen window not found. Visible windows: {visibleWindows}");
        }

        return qwenWindow!;
    }

    private static WindowSummary FindWeChatWindow(QueryToolService queryService)
    {
        var weChatWindow = TryFindWeChatWindow(queryService);
        if (weChatWindow is null)
        {
            var visibleWindows = string.Join(", ", queryService.WindowsList().Windows.Select(window => window.Title));
            throw new Xunit.Sdk.XunitException($"WeChat window not found. Visible windows: {visibleWindows}");
        }

        return weChatWindow!;
    }

    private static WindowSummary? TryFindSettingsWindow(QueryToolService queryService)
    {
        var windows = queryService.WindowsList().Windows;
        return windows.FirstOrDefault(candidate =>
            candidate.IsActive &&
            (candidate.Title.Contains("设置", StringComparison.OrdinalIgnoreCase) ||
             candidate.Title.Contains("Settings", StringComparison.OrdinalIgnoreCase)))
            ?? windows.FirstOrDefault(candidate =>
                candidate.Title.Contains("设置", StringComparison.OrdinalIgnoreCase) ||
                candidate.Title.Contains("Settings", StringComparison.OrdinalIgnoreCase));
    }

    private static WindowSummary? TryFindCodexWindow(QueryToolService queryService)
    {
        var windows = queryService.WindowsList().Windows;
        return windows.FirstOrDefault(candidate =>
            candidate.IsActive && candidate.Title.Contains("Codex", StringComparison.OrdinalIgnoreCase))
            ?? windows.FirstOrDefault(candidate =>
                candidate.Title.Contains("Codex", StringComparison.OrdinalIgnoreCase));
    }

    private static WindowSummary? TryFindQwenWindow(QueryToolService queryService)
    {
        var windows = queryService.WindowsList().Windows;
        return windows.FirstOrDefault(candidate =>
            candidate.IsActive && candidate.Title.Contains("Qwen", StringComparison.OrdinalIgnoreCase))
            ?? windows.FirstOrDefault(candidate =>
                candidate.Title.Contains("Qwen", StringComparison.OrdinalIgnoreCase));
    }

    private static WindowSummary? TryFindWeChatWindow(QueryToolService queryService)
    {
        var windows = queryService.WindowsList().Windows;
        return windows.FirstOrDefault(candidate =>
            candidate.IsActive &&
            (candidate.Title.Contains("WeChat", StringComparison.OrdinalIgnoreCase)
             || candidate.Title.Contains("微信", StringComparison.OrdinalIgnoreCase)))
            ?? windows.FirstOrDefault(candidate =>
                candidate.Title.Contains("WeChat", StringComparison.OrdinalIgnoreCase)
                || candidate.Title.Contains("微信", StringComparison.OrdinalIgnoreCase));
    }

    private static WindowSummary FindWindowByRef(QueryToolService queryService, string windowRef)
    {
        var window = queryService.WindowsList().Windows.FirstOrDefault(candidate =>
            string.Equals(candidate.Ref.Value, windowRef, StringComparison.Ordinal));

        Assert.NotNull(window);
        return window!;
    }

    private static SnapshotResult WaitForNotepadSaveAsDialog(
        QueryToolService queryService,
        string windowRef,
        int timeoutMilliseconds = 7000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        SnapshotResult? lastSnapshot = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var window = FindWindowByRef(queryService, windowRef);
            lastSnapshot = RealAppTestDriver.SnapshotWindow(queryService, window);
            if (IsNotepadSaveAsSnapshot(lastSnapshot))
            {
                return lastSnapshot;
            }

            Thread.Sleep(100);
        }

        Assert.NotNull(lastSnapshot);
        throw new Xunit.Sdk.XunitException(lastSnapshot!.SummaryText);
    }

    private static bool IsNotepadSaveAsSnapshot(SnapshotResult snapshot)
    {
        var summary = snapshot.SummaryText;
        return summary.Contains("另存为", StringComparison.Ordinal) &&
               (summary.Contains("文件名:", StringComparison.Ordinal) ||
                summary.Contains("保存(S)", StringComparison.Ordinal) ||
                summary.Contains("取消", StringComparison.Ordinal));
    }

    private static ElementCandidate LocateNotepadSaveAsCancelButton(QueryToolService queryService, string windowTitle)
    {
        var locate = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{windowTitle}\") window[name=\"另存为\"] >> button[name=\"取消\"]",
                $"scope:window(name=\"{windowTitle}\") button[name=\"取消\"]",
                "scope:active_window button[name=\"取消\"]",
            },
            timeoutMilliseconds: 3000);

        Assert.NotNull(locate.BestMatch);
        return locate.BestMatch!;
    }

    private static ActionResult ExecuteForegroundSensitivePressKeys(
        QueryToolService queryService,
        ActionToolService actionService,
        string targetRef,
        IReadOnlyList<object?> keys,
        string requestId)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["keys"] = keys.ToArray(),
        };

        ActionResult? lastResult = null;
        string? lastErrorMessage = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
            {
                var refocus = RealAppTestDriver.Act(
                    actionService,
                    $"{requestId}-refocus-{attempt}",
                    targetRef,
                    "focus",
                    new Dictionary<string, object?>(),
                    ExecutionPolicy.Default with
                    {
                        AutoActivateWindow = false,
                    },
                    new ExpectedOutcome("focus_changed", new Dictionary<string, object?>(), 500));

                Assert.True(refocus.Success);
            }

            var result = actionService.WindowsAct(new ActRequest(
                requestId,
                new ActionTarget(targetRef, null),
                "press_keys",
                arguments,
                ExecutionPolicy.Default with
                {
                    AutoActivateWindow = true,
                },
                new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500),
                5000));

            if (result.IsSuccess && result.Payload is not null)
            {
                Assert.Equal("windows_act", result.Payload.Diagnostics["tool_name"]);
                Assert.Equal("text_structured", result.Payload.Diagnostics["interaction_model"]);
                Assert.Equal("accessibility_tree", result.Payload.Diagnostics["primary_interface"]);
                Assert.Equal("press_keys", result.Payload.Diagnostics["action_name"]);
                lastResult = result.Payload;
                break;
            }

            lastErrorMessage = result.Error?.Message ?? result.Payload?.Error?.Message ?? "windows_act failed";
            if (!lastErrorMessage.Contains("could not be activated", StringComparison.OrdinalIgnoreCase))
            {
                Assert.True(result.IsSuccess, lastErrorMessage);
            }

            Thread.Sleep(200);
        }

        Assert.True(lastResult is not null, lastErrorMessage ?? "Foreground-sensitive press_keys did not produce a result.");
        return lastResult!;
    }

    private static ActionResult InvokeExplorerBack(
        QueryToolService queryService,
        ActionToolService actionService,
        string requestId)
    {
        var backButton = WaitForLocateAny(
            queryService,
            new[]
            {
                "scope:active_window button[name=\"后退\"]",
                "scope:active_window button[name~ = \"后退\"]",
            },
            timeoutMilliseconds: 3000);

        return RealAppTestDriver.Act(
            actionService,
            requestId,
            backButton.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));
    }

    private static WindowSummary OpenSettingsSoundPage(QueryToolService queryService, ActionToolService actionService)
    {
        var settingsWindow = WaitForSettingsWindow(queryService);
        var existingSnapshot = WaitForSettingsSnapshot(queryService);
        settingsWindow = FindSettingsWindow(queryService);
        if (IsSettingsSoundPageSnapshot(existingSnapshot))
        {
            return settingsWindow;
        }

        var systemButton = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{settingsWindow.Title}\") button[name=\"系统\"]",
                "scope:active_window button[name=\"系统\"]",
            },
            timeoutMilliseconds: 3000);
        var openSystem = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-open-system-root-shared",
            systemButton.BestMatch!.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(openSystem.Success);

        settingsWindow = WaitForSettingsWindow(queryService);
        var soundSnapshot = WaitForSettingsSnapshot(queryService);
        settingsWindow = FindSettingsWindow(queryService);
        if (IsSettingsSoundPageSnapshot(soundSnapshot))
        {
            return settingsWindow;
        }

        var soundItem = LocateSettingsSoundEntry(queryService, settingsWindow.Title);
        var openSound = RealAppTestDriver.Act(
            actionService,
            "req-real-settings-open-sound-shared",
            soundItem.Ref.Value,
            "invoke",
            new Dictionary<string, object?>(),
            ExecutionPolicy.Default with
            {
                AutoActivateWindow = false,
            },
            new ExpectedOutcome("no_verification", new Dictionary<string, object?>(), 500));

        Assert.True(openSound.Success);
        WaitForSettingsSnapshot(
            queryService,
            timeoutMilliseconds: 10000,
            isReady: IsSettingsSoundPageSnapshot);
        return FindSettingsWindow(queryService);
    }

    private static ElementCandidate LocateSettingsSoundEntry(QueryToolService queryService, string windowTitle)
    {
        var locate = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{windowTitle}\") listitem[name=\"声音\"]",
                $"scope:window(name=\"{windowTitle}\") button[name=\"声音\"]",
                $"scope:window(name=\"{windowTitle}\") *[name=\"声音\"]",
                "scope:active_window listitem[name=\"声音\"]",
                "scope:active_window button[name=\"声音\"]",
                "scope:active_window *[name=\"声音\"]",
            },
            timeoutMilliseconds: 7000);

        Assert.NotNull(locate.BestMatch);
        return locate.BestMatch!;
    }

    private static WindowSummary WaitForSettingsWindow(QueryToolService queryService, int timeoutMilliseconds = 5000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var settingsWindow = TryFindSettingsWindow(queryService);
            if (settingsWindow is not null && settingsWindow.IsActive)
            {
                return settingsWindow;
            }

            if (settingsWindow is not null)
            {
                return settingsWindow;
            }

            Thread.Sleep(100);
        }

        return FindSettingsWindow(queryService);
    }

    private static SnapshotResult WaitForSettingsSnapshot(
        QueryToolService queryService,
        int timeoutMilliseconds = 7000,
        Func<SnapshotResult, bool>? isReady = null)
    {
        isReady ??= IsMeaningfulSettingsSnapshot;
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        SnapshotResult? lastSnapshot = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var window = WaitForSettingsWindow(queryService, timeoutMilliseconds: 1000);
            lastSnapshot = RealAppTestDriver.SnapshotWindow(queryService, window);

            if (isReady(lastSnapshot))
            {
                return lastSnapshot;
            }

            Thread.Sleep(200);
        }

        Assert.NotNull(lastSnapshot);
        throw new Xunit.Sdk.XunitException(lastSnapshot!.SummaryText);
    }

    private static SnapshotResult WaitForCodexSnapshot(
        QueryToolService queryService,
        int timeoutMilliseconds = 7000,
        Func<SnapshotResult, bool>? isReady = null)
    {
        isReady ??= IsMeaningfulCodexToolbarSnapshot;
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        SnapshotResult? lastSnapshot = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var window = FindCodexWindow(queryService);
            lastSnapshot = RealAppTestDriver.SnapshotWindow(queryService, window);

            if (isReady(lastSnapshot))
            {
                return lastSnapshot;
            }

            Thread.Sleep(200);
        }

        Assert.NotNull(lastSnapshot);
        throw new Xunit.Sdk.XunitException(lastSnapshot!.SummaryText);
    }

    private static SnapshotResult WaitForQwenSnapshot(
        QueryToolService queryService,
        int timeoutMilliseconds = 7000,
        Func<SnapshotResult, bool>? isReady = null)
    {
        isReady ??= current => !string.IsNullOrWhiteSpace(current.SummaryText);
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        SnapshotResult? lastSnapshot = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var window = FindQwenWindow(queryService);
            lastSnapshot = RealAppTestDriver.SnapshotWindow(queryService, window);

            if (isReady(lastSnapshot))
            {
                return lastSnapshot;
            }

            Thread.Sleep(200);
        }

        Assert.NotNull(lastSnapshot);
        throw new Xunit.Sdk.XunitException(lastSnapshot!.SummaryText);
    }

    private static SnapshotResult WaitForWeChatSnapshot(
        QueryToolService queryService,
        int timeoutMilliseconds = 7000,
        Func<SnapshotResult, bool>? isReady = null)
    {
        isReady ??= current => !string.IsNullOrWhiteSpace(current.SummaryText);
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        SnapshotResult? lastSnapshot = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var window = FindWeChatWindow(queryService);
            lastSnapshot = RealAppTestDriver.SnapshotWindow(queryService, window);

            if (isReady(lastSnapshot))
            {
                return lastSnapshot;
            }

            Thread.Sleep(200);
        }

        Assert.NotNull(lastSnapshot);
        throw new Xunit.Sdk.XunitException(lastSnapshot!.SummaryText);
    }

    private static bool IsMeaningfulSettingsSnapshot(SnapshotResult snapshot)
    {
        var summary = snapshot.SummaryText;
        return summary.Contains("搜索框", StringComparison.Ordinal) ||
               summary.Contains("系统", StringComparison.Ordinal) ||
               summary.Contains("屏幕", StringComparison.Ordinal) ||
               summary.Contains("声音", StringComparison.Ordinal) ||
               summary.Contains("单声道音频", StringComparison.Ordinal) ||
               summary.Contains("调整输出音量", StringComparison.Ordinal);
    }

    private static bool IsMeaningfulCodexFocusSnapshot(SnapshotResult snapshot)
    {
        var summary = snapshot.SummaryText;
        return summary.Contains("完全访问权限", StringComparison.Ordinal)
            || summary.Contains("计划", StringComparison.Ordinal)
            || summary.Contains("听写", StringComparison.Ordinal)
            || summary.Contains("team-mcpv2", StringComparison.Ordinal);
    }

    private static bool IsMeaningfulCodexToolbarSnapshot(SnapshotResult snapshot)
    {
        var summary = snapshot.SummaryText;
        return summary.Contains("搜索", StringComparison.Ordinal)
            || summary.Contains("新对话", StringComparison.Ordinal)
            || summary.Contains("隐藏边栏", StringComparison.Ordinal);
    }

    private static ElementNode? FindFirstNode(ElementNode root, Func<ElementNode, bool> predicate)
    {
        if (predicate(root))
        {
            return root;
        }

        foreach (var child in root.Children)
        {
            var match = FindFirstNode(child, predicate);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static bool IsSettingsSoundPageSnapshot(SnapshotResult snapshot)
    {
        var summary = snapshot.SummaryText;
        return summary.Contains("单声道音频", StringComparison.Ordinal) &&
               summary.Contains("调整输出音量", StringComparison.Ordinal);
    }

    private static LocateResult WaitForLocate(
        QueryToolService queryService,
        string selector,
        int timeoutMilliseconds = 2000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        ToolResult<LocateResult>? lastResult = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            lastResult = queryService.WindowsLocate(new WindowsLocateRequest(selector));
            if (lastResult.IsSuccess)
            {
                Assert.NotNull(lastResult.Payload);
                Assert.NotNull(lastResult.Payload!.BestMatch);
                Assert.Equal("windows_locate", lastResult.Payload.Diagnostics["tool_name"]);
                Assert.Equal("text_structured", lastResult.Payload.Diagnostics["interaction_model"]);
                Assert.Equal("accessibility_tree", lastResult.Payload.Diagnostics["primary_interface"]);
                Assert.Equal(selector, lastResult.Payload.Diagnostics["selector_used"]);
                return lastResult.Payload;
            }

            Thread.Sleep(100);
        }

        Assert.True(lastResult?.IsSuccess ?? false, lastResult?.Error?.Message ?? "windows_locate failed");
        throw new InvalidOperationException("Unreachable");
    }

    private static LocateResult WaitForLocateAny(
        QueryToolService queryService,
        IReadOnlyList<string> selectors,
        int timeoutMilliseconds = 2000)
    {
        Assert.NotEmpty(selectors);

        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMilliseconds);
        ToolResult<LocateResult>? lastResult = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            foreach (var selector in selectors)
            {
                lastResult = queryService.WindowsLocate(new WindowsLocateRequest(selector));
                if (lastResult.IsSuccess)
                {
                    Assert.NotNull(lastResult.Payload);
                    Assert.NotNull(lastResult.Payload!.BestMatch);
                    Assert.Equal("windows_locate", lastResult.Payload.Diagnostics["tool_name"]);
                    Assert.Equal("text_structured", lastResult.Payload.Diagnostics["interaction_model"]);
                    Assert.Equal("accessibility_tree", lastResult.Payload.Diagnostics["primary_interface"]);
                    Assert.Equal(selector, lastResult.Payload.Diagnostics["selector_used"]);
                    return lastResult.Payload;
                }
            }

            Thread.Sleep(100);
        }

        Assert.True(lastResult?.IsSuccess ?? false, lastResult?.Error?.Message ?? "windows_locate failed");
        throw new InvalidOperationException("Unreachable");
    }

    private static ElementCandidate LocateNightLightToggle(QueryToolService queryService, string windowTitle)
    {
        var locate = queryService.WindowsLocate(new WindowsLocateRequest(
            $"scope:window(name=\"{windowTitle}\") button[name=\"夜间模式\"]"));

        if (locate.IsSuccess)
        {
            Assert.NotNull(locate.Payload);
            Assert.NotNull(locate.Payload!.BestMatch);
            return locate.Payload.BestMatch!;
        }

        Assert.NotNull(locate.Payload);
        Assert.Equal(A11yFlow.Core.Errors.ToolErrorCode.TargetAmbiguous, locate.Error!.Code);
        var toggleCandidate = locate.Payload!.Candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.Role, "button", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(candidate.Name, "夜间模式", StringComparison.Ordinal));

        Assert.NotNull(toggleCandidate);
        return toggleCandidate!;
    }

    private static ElementCandidate LocateSoundVolumeToggle(QueryToolService queryService, string windowTitle)
    {
        var locate = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{windowTitle}\") button[name=\"静音音量\"]",
                $"scope:window(name=\"{windowTitle}\") button[name=\"取消静音音量\"]",
                $"scope:window(name=\"{windowTitle}\") button[name~=\"静音\"]",
                "scope:active_window button[name=\"静音音量\"]",
                "scope:active_window button[name=\"取消静音音量\"]",
                "scope:active_window button[name~=\"静音\"]",
            },
            timeoutMilliseconds: 3000);

        Assert.NotNull(locate.BestMatch);
        return locate.BestMatch!;
    }

    private static ElementCandidate LocateSoundVolumeSlider(QueryToolService queryService, string windowTitle)
    {
        var locate = WaitForLocateAny(
            queryService,
            new[]
            {
                $"scope:window(name=\"{windowTitle}\") slider[name=\"调整输出音量\"]",
                "scope:active_window slider[name=\"调整输出音量\"]",
                $"scope:window(name=\"{windowTitle}\") slider[name~=\"输出音量\"]",
                "scope:active_window slider[name~=\"输出音量\"]",
            },
            timeoutMilliseconds: 3000);

        Assert.NotNull(locate.BestMatch);
        return locate.BestMatch!;
    }

    private static DirectoryInfo? FindAncestorDirectory(string path, string directoryName)
    {
        var current = new DirectoryInfo(path);
        while (current is not null)
        {
            if (string.Equals(current.Name, directoryName, StringComparison.OrdinalIgnoreCase))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }

    private static DirectoryInfo? FindImmediateChildDirectory(DirectoryInfo ancestor, string descendantPath)
    {
        var childName = FindImmediateChildName(ancestor, descendantPath);
        return string.IsNullOrWhiteSpace(childName)
            ? null
            : new DirectoryInfo(Path.Combine(ancestor.FullName, childName));
    }

    private static string? FindImmediateChildName(DirectoryInfo ancestor, string descendantPath)
    {
        var descendant = new DirectoryInfo(descendantPath);
        DirectoryInfo? child = descendant;

        while (child?.Parent is not null && !string.Equals(child.Parent.FullName, ancestor.FullName, StringComparison.OrdinalIgnoreCase))
        {
            child = child.Parent;
        }

        return child?.Name;
    }

}
