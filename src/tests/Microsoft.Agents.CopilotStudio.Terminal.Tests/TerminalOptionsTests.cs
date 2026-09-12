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
        Assert.Contains("Exit codes:", TerminalOptions.Usage);
        Assert.Contains("1 configuration/startup failure", TerminalOptions.Usage);
        Assert.Contains("2 option/terminal usage error", TerminalOptions.Usage);
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
