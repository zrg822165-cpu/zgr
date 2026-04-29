using OpenClaw.Core.Locators;

namespace OpenClaw.Tests.Unit.Locators;

public sealed class SelectorParserTests
{
    [Fact]
    public void Parse_StructuralSelector_BuildsScopeSegmentsAndRelations()
    {
        var parser = new SelectorParser();

        var result = parser.Parse("scope:window(name=\"设置\") group[name=\"代理\"] >> button[name=\"保存\"]");

        Assert.True(result.IsSuccess);
        var selector = Assert.Single(result.Alternatives);
        Assert.Equal(SelectorScopeKind.WindowByName, selector.Scope.Kind);
        Assert.Equal("设置", selector.Scope.WindowName);
        Assert.Single(selector.Relations);
        Assert.Equal(SelectorRelation.Descendant, selector.Relations[0]);
        Assert.Equal("group", selector.Segments[0].Role);
        Assert.Equal("button", selector.Segments[1].Role);
    }

    [Fact]
    public void Parse_FallbackChain_BuildsMultipleAlternatives()
    {
        var parser = new SelectorParser();

        var result = parser.Parse("button[automation_id=\"saveBtn\"] || button:text(\"保存\")");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Alternatives.Count);
        Assert.Null(result.Alternatives[0].Segments[0].Text);
        Assert.NotNull(result.Alternatives[1].Segments[0].Text);
    }

    [Fact]
    public void Parse_InvalidSelector_ReturnsErrorPosition()
    {
        var parser = new SelectorParser();

        var result = parser.Parse("button[name=\"保存\"");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.True(result.Error!.Position > 0);
    }
}
