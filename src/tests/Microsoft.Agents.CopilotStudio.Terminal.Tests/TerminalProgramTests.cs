#nullable enable

using System.IO;
using System.Threading.Tasks;

public sealed class TerminalProgramTests
{
    [Fact]
    public async Task RunAsync_HelpReturnsZeroBeforeInteractiveTerminalChecks()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await TerminalProgram.RunAsync(
            ["--help"],
            inputRedirected: true,
            outputRedirected: true,
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task RunAsync_InvalidOptionReturnsTwoWithErrorAndUsage()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await TerminalProgram.RunAsync(
            ["--unknown"],
            inputRedirected: false,
            outputRedirected: false,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("Unknown option", error.ToString());
        Assert.Contains("Usage:", error.ToString());
    }

    [Fact]
    public async Task RunAsync_RedirectedTerminalReturnsTwoWithoutBuildingHost()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await TerminalProgram.RunAsync(
            [],
            inputRedirected: false,
            outputRedirected: true,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("interactive terminal", error.ToString());
    }
}
