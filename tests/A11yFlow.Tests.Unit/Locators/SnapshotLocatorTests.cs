using A11yFlow.Core.Locators;
using A11yFlow.Core.Models;
using A11yFlow.Core.Refs;
using A11yFlow.Core.Snapshots;

namespace A11yFlow.Tests.Unit.Locators;

public sealed class SnapshotLocatorTests
{
    private readonly SelectorParser _parser = new();
    private readonly SnapshotLocator _locator = new();

    [Fact]
    public void Locate_StrictSelector_ReturnsSingleMatch()
    {
        var snapshot = CreateSettingsSnapshot();

        var result = _locator.Locate(snapshot, _parser.Parse("scope:active_window button[name=\"保存\"]"));

        Assert.Equal(LocateStatus.Found, result.Status);
        Assert.NotNull(result.BestMatch);
        Assert.Equal("w1e5", result.BestMatch!.Ref.Value);
        Assert.Equal("strict_match", result.Diagnostics["strategy_used"]);
    }

    [Fact]
    public void Locate_MultipleMatches_ReturnsAmbiguous()
    {
        var snapshot = CreateAmbiguousSnapshot();

        var result = _locator.Locate(snapshot, _parser.Parse("scope:active_window button[name=\"确定\"]"));

        Assert.Equal(LocateStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Candidates.Count);
    }

    [Fact]
    public void Locate_StructuralSelector_UsesStructuralStrategy()
    {
        var snapshot = CreateSettingsSnapshot();

        var result = _locator.Locate(snapshot, _parser.Parse("scope:window(name=\"设置\") group[name=\"代理\"] > button[name=\"保存\"]"));

        Assert.Equal(LocateStatus.Found, result.Status);
        Assert.Equal("structural_match", result.Diagnostics["strategy_used"]);
    }

    [Fact]
    public void Locate_DescendantSelector_UsesDescendantStrategy()
    {
        var snapshot = CreateNestedSettingsSnapshot();

        var result = _locator.Locate(snapshot, _parser.Parse("scope:window(name=\"设置\") group[name=\"代理\"] >> button[name=\"保存\"]"));

        Assert.Equal(LocateStatus.Found, result.Status);
        Assert.Equal("descendant_search", result.Diagnostics["strategy_used"]);
    }

    [Fact]
    public void Locate_TextSelector_UsesTextStrategy()
    {
        var snapshot = CreateSettingsSnapshot();

        var result = _locator.Locate(snapshot, _parser.Parse("button:text(\"保存\")"));

        Assert.Equal(LocateStatus.Found, result.Status);
        Assert.Equal("text_match", result.Diagnostics["strategy_used"]);
    }

    [Fact]
    public void Locate_FallbackChain_UsesSecondAlternative()
    {
        var snapshot = CreateSettingsSnapshot();

        var result = _locator.Locate(snapshot, _parser.Parse("button[automation_id=\"saveBtn\"] || button:text(\"保存\")"));

        Assert.Equal(LocateStatus.Found, result.Status);
        Assert.Equal("1", result.Diagnostics["selector_alternative"]);
        Assert.Contains("alt[0] no_match", result.Diagnostics["attempts"]);
        Assert.Contains("alt[1] matched", result.Diagnostics["attempts"]);
    }

    [Fact]
    public void Describe_ReturnsParentAndChildContext()
    {
        var snapshot = CreateSettingsSnapshot();

        var description = _locator.Describe(snapshot.Root, snapshot.WindowRef, "w1e5");

        Assert.NotNull(description);
        Assert.Equal("button", description!.Role);
        Assert.Equal("w1e2", description.ParentRef!.Value);
        Assert.Empty(description.ChildRefs);
    }

    private static SnapshotResult CreateSettingsSnapshot()
    {
        var root = Node(
            "w1e1",
            "window",
            "设置",
            children:
            [
                Node(
                    "w1e2",
                    "group",
                    "代理",
                    children:
                    [
                        Node("w1e3", "text", "代理地址"),
                        Node("w1e4", "edit", "代理地址", automationId: "ProxyAddress", actions: ["focus", "set_value"]),
                        Node("w1e5", "button", "保存", actions: ["invoke", "focus"]),
                    ])
            ]);

        return new SnapshotResult(new WindowRef("w1"), "snap-1", root, "summary", new Dictionary<string, string?>());
    }

    private static SnapshotResult CreateNestedSettingsSnapshot()
    {
        var root = Node(
            "w1e1",
            "window",
            "设置",
            children:
            [
                Node(
                    "w1e2",
                    "group",
                    "代理",
                    children:
                    [
                        Node(
                            "w1e3",
                            "pane",
                            "代理内容",
                            children:
                            [
                                Node("w1e4", "button", "保存", actions: ["invoke"]),
                            ])
                    ])
            ]);

        return new SnapshotResult(new WindowRef("w1"), "snap-2", root, "summary", new Dictionary<string, string?>());
    }

    private static SnapshotResult CreateAmbiguousSnapshot()
    {
        var root = Node(
            "w1e1",
            "window",
            "对话框",
            children:
            [
                Node("w1e2", "button", "确定", actions: ["invoke"]),
                Node("w1e3", "button", "确定", actions: ["invoke"]),
            ]);

        return new SnapshotResult(new WindowRef("w1"), "snap-3", root, "summary", new Dictionary<string, string?>());
    }

    private static ElementNode Node(
        string reference,
        string role,
        string? name,
        string? automationId = null,
        string? className = null,
        IReadOnlyList<string>? states = null,
        IReadOnlyList<string>? actions = null,
        IReadOnlyList<ElementNode>? children = null)
    {
        return new ElementNode(
            new ElementRef(reference),
            role,
            name,
            automationId,
            className,
            null,
            states ?? Array.Empty<string>(),
            actions ?? Array.Empty<string>(),
            children ?? Array.Empty<ElementNode>());
    }
}
