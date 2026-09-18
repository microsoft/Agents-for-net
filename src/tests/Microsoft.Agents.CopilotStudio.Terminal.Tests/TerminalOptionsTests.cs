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

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_Help_DoesNotRequireLayout(string option)
    {
        TerminalOptions result = TerminalOptions.Parse([option]);
        Assert.True(result.ShowHelp);
        Assert.StartsWith(
            "Usage: CopilotStudioClient.Terminal.exe [options]",
            TerminalOptions.Usage);
        Assert.DoesNotContain("dotnet run --project", TerminalOptions.Usage);
        Assert.Contains("Exit codes:", TerminalOptions.Usage);
        Assert.Contains("1 configuration/startup failure", TerminalOptions.Usage);
        Assert.Contains("2 option/terminal usage error", TerminalOptions.Usage);
    }

    [Fact]
    public void Parse_AllLongConnectionOptions_AcceptsValuesWithoutPrintingSecret()
    {
        const string secret = "long-option-secret";

        TerminalOptions result = TerminalOptions.Parse(
        [
            "--tenant-id", "tenant",
            "--app-client-id", "client",
            "--app-client-secret", secret,
            "--direct-connect-url", "https://example.com/direct",
            "--environment-id", "environment",
            "--schema-name", "schema",
            "--use-s2s-connection", "true"
        ]);

        Assert.DoesNotContain(secret, result.ToString());
    }

    [Fact]
    public void Parse_AllConnectionAliases_AcceptsValuesWithoutPrintingSecret()
    {
        const string secret = "short-option-secret";

        TerminalOptions result = TerminalOptions.Parse(
        [
            "-t", "tenant",
            "-c", "client",
            "-k", secret,
            "-d", "https://example.com/direct",
            "-e", "environment",
            "-s", "schema",
            "-u", "false"
        ]);

        Assert.DoesNotContain(secret, result.ToString());
    }

    [Theory]
    [InlineData("--tenant-id")]
    [InlineData("-t")]
    [InlineData("--app-client-id")]
    [InlineData("-c")]
    [InlineData("--app-client-secret")]
    [InlineData("-k")]
    [InlineData("--direct-connect-url")]
    [InlineData("-d")]
    [InlineData("--environment-id")]
    [InlineData("-e")]
    [InlineData("--schema-name")]
    [InlineData("-s")]
    [InlineData("--use-s2s-connection")]
    [InlineData("-u")]
    public void Parse_ConnectionOptionWithoutValue_ThrowsOptionException(string option)
    {
        TerminalOptionException error = Assert.Throws<TerminalOptionException>(
            () => TerminalOptions.Parse([option]));

        Assert.Contains(option, error.Message);
        Assert.Contains("requires", error.Message);
    }

    [Theory]
    [InlineData("--tenant-id", "--app-client-id")]
    [InlineData("-k", "--help")]
    [InlineData("--layout", "-h")]
    public void Parse_OptionFollowedByRecognizedOption_DoesNotConsumeItAsValue(
        string option,
        string nextOption)
    {
        TerminalOptionException error = Assert.Throws<TerminalOptionException>(
            () => TerminalOptions.Parse([option, nextOption]));

        Assert.Contains(option, error.Message);
        Assert.Contains("requires", error.Message);
        Assert.DoesNotContain(nextOption, error.Message);
    }

    [Theory]
    [InlineData("--use-s2s-connection")]
    [InlineData("-u")]
    public void Parse_InvalidBoolean_ThrowsWithoutPrintingValue(string option)
    {
        const string invalidValue = "not-a-boolean";

        TerminalOptionException error = Assert.Throws<TerminalOptionException>(
            () => TerminalOptions.Parse([option, invalidValue]));

        Assert.Contains(option, error.Message);
        Assert.Contains("true", error.Message);
        Assert.Contains("false", error.Message);
        Assert.DoesNotContain(invalidValue, error.Message);
    }

    [Fact]
    public void Parse_WhitespaceStringValue_IsAcceptedForMergedValidation()
    {
        TerminalOptions result = TerminalOptions.Parse(["--tenant-id", " \t"]);

        Assert.NotNull(result);
    }

    [Fact]
    public void Parse_UnknownLayout_ThrowsOptionException()
    {
        const string invalidValue = "drawer";

        TerminalOptionException error = Assert.Throws<TerminalOptionException>(
            () => TerminalOptions.Parse(["--layout", invalidValue]));
        Assert.Contains("tabs", error.Message);
        Assert.Contains("split", error.Message);
        Assert.DoesNotContain(invalidValue, error.Message);
    }

    [Theory]
    [InlineData("--tenant-id", "-t")]
    [InlineData("--app-client-id", "-c")]
    [InlineData("--app-client-secret", "-k")]
    [InlineData("--direct-connect-url", "-d")]
    [InlineData("--environment-id", "-e")]
    [InlineData("--schema-name", "-s")]
    [InlineData("--use-s2s-connection", "-u")]
    public void Usage_ListsConnectionOptionAndAlias(string option, string alias)
    {
        Assert.Contains(option, TerminalOptions.Usage);
        Assert.Contains(alias, TerminalOptions.Usage);
    }
}
