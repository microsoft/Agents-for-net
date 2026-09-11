public sealed class TerminalOptionsTests
{
    [Fact]
    public void Parse_NoArguments_UsesTabs()
    {
        TerminalOptions result = TerminalOptions.Parse([]);
        Assert.Equal(TerminalLayout.Tabs, result.Layout);
        Assert.False(result.ShowHelp);
    }

    [Fact]
    public void Parse_LayoutTabs_UsesTabs()
    {
        TerminalOptions result = TerminalOptions.Parse(["--layout", "tabs"]);
        Assert.Equal(TerminalLayout.Tabs, result.Layout);
    }

    [Fact]
    public void Parse_LayoutSplit_UsesSplit()
    {
        TerminalOptions result = TerminalOptions.Parse(["--layout", "split"]);
        Assert.Equal(TerminalLayout.Split, result.Layout);
    }

    [Fact]
    public void Parse_Help_DoesNotRequireLayout()
    {
        TerminalOptions result = TerminalOptions.Parse(["--help"]);
        Assert.True(result.ShowHelp);
    }

    [Fact]
    public void Parse_UnknownLayout_ThrowsOptionException()
    {
        TerminalOptionException error = Assert.Throws<TerminalOptionException>(
            () => TerminalOptions.Parse(["--layout", "drawer"]));
        Assert.Contains("tabs", error.Message);
        Assert.Contains("split", error.Message);
    }
}
