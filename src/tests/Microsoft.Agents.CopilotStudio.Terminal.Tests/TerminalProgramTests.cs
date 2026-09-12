#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    [Fact]
    public async Task RunConfiguredAsync_InvalidConfigurationReturnsOneWithoutStartingTerminal()
    {
        HostApplicationBuilder builder = CreateBuilder(
            new Dictionary<string, string?>
            {
                ["CopilotStudioClientSettings:DirectConnectUrl"] = "",
                ["CopilotStudioClientSettings:EnvironmentId"] = "",
                ["CopilotStudioClientSettings:SchemaName"] = "",
                ["CopilotStudioClientSettings:TenantId"] = "",
                ["CopilotStudioClientSettings:UseS2SConnection"] = "false",
                ["CopilotStudioClientSettings:AppClientId"] = "",
                ["CopilotStudioClientSettings:AppClientSecret"] = ""
            });
        StringWriter error = new();
        bool terminalStarted = false;

        int exitCode = await TerminalProgram.RunConfiguredAsync(
            new TerminalOptions(TerminalLayout.Tabs, false),
            builder,
            error,
            (_, _) =>
            {
                terminalStarted = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.False(terminalStarted);
        Assert.Contains("Startup failed:", error.ToString());
        Assert.Contains("AppClientId", error.ToString());
    }

    [Fact]
    public async Task RunConfiguredAsync_ValidConfigurationStartsTerminal()
    {
        HostApplicationBuilder builder = CreateBuilder(
            new Dictionary<string, string?>
            {
                ["CopilotStudioClientSettings:DirectConnectUrl"] = "https://example.com/direct",
                ["CopilotStudioClientSettings:TenantId"] = "tenant",
                ["CopilotStudioClientSettings:UseS2SConnection"] = "false",
                ["CopilotStudioClientSettings:AppClientId"] = "client"
            });
        StringWriter error = new();
        bool terminalStarted = false;

        int exitCode = await TerminalProgram.RunConfiguredAsync(
            new TerminalOptions(TerminalLayout.Tabs, false),
            builder,
            error,
            (services, _) =>
            {
                Assert.NotNull(services.GetRequiredService<TerminalChatApplication>());
                Assert.NotNull(services.GetRequiredService<TerminalPresenter>());
                terminalStarted = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(exitCode == 0, error.ToString());
        Assert.True(terminalStarted);
        Assert.Equal(string.Empty, error.ToString());
    }

    private static HostApplicationBuilder CreateBuilder(
        IReadOnlyDictionary<string, string?> values)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(
            new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }
}
