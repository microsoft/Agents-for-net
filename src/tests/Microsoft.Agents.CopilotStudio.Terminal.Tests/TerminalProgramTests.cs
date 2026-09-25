#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CopilotStudioClient.Terminal;
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
    public async Task RunAsync_UnknownOptionDoesNotPrintSuppliedValue()
    {
        const string secret = "--credential=secret-must-not-be-printed";
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await TerminalProgram.RunAsync(
            [secret],
            inputRedirected: false,
            outputRedirected: false,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Contains("Unknown option", error.ToString());
        Assert.DoesNotContain(secret, error.ToString());
    }

    [Fact]
    public async Task RunAsync_InvalidBooleanReturnsTwoWithoutPrintingSuppliedValues()
    {
        const string secret = "program-secret-must-not-be-printed";
        const string invalidBoolean = "invalid-boolean-must-not-be-printed";
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await TerminalProgram.RunAsync(
            [
                "--app-client-secret", secret,
                "--use-s2s-connection", invalidBoolean
            ],
            inputRedirected: false,
            outputRedirected: false,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("--use-s2s-connection", error.ToString());
        Assert.DoesNotContain(secret, error.ToString());
        Assert.DoesNotContain(invalidBoolean, error.ToString());
    }

    [Fact]
    public async Task RunAsync_MissingConnectionValueReturnsTwo()
    {
        StringWriter output = new();
        StringWriter error = new();

        int exitCode = await TerminalProgram.RunAsync(
            ["--tenant-id", "--app-client-id", "client"],
            inputRedirected: false,
            outputRedirected: false,
            output,
            error);

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, output.ToString());
        Assert.Contains("--tenant-id", error.ToString());
        Assert.Contains("requires", error.ToString());
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

    [Fact]
    public async Task RunConfiguredAsync_LongCliValuesOverrideConfigurationAndDirectUrlClearsCoordinates()
    {
        const string cliSecret = "cli-secret";
        RunResult result = await RunConfiguredAsync(
        [
            "--tenant-id", "cli-tenant",
            "--app-client-id", "cli-client",
            "--app-client-secret", cliSecret,
            "--direct-connect-url", "https://example.com/cli-direct",
            "--environment-id", "cli-environment",
            "--schema-name", "cli-schema",
            "--use-s2s-connection", "false"
        ],
        CreateValidConfiguration(
            directConnectUrl: "https://example.com/config-direct",
            environmentId: "config-environment",
            schemaName: "config-schema",
            tenantId: "config-tenant",
            useS2SConnection: true,
            appClientId: "config-client",
            appClientSecret: "config-secret"));

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.True(result.TerminalStarted);
        Assert.NotNull(result.Settings);
        Assert.Equal("cli-tenant", result.Settings.TenantId);
        Assert.Equal("cli-client", result.Settings.AppClientId);
        Assert.Equal(cliSecret, result.Settings.AppClientSecret);
        Assert.Equal("https://example.com/cli-direct", result.Settings.DirectConnectUrl);
        Assert.Null(result.Settings.EnvironmentId);
        Assert.Null(result.Settings.SchemaName);
        Assert.False(result.Settings.UseS2SConnection);
    }

    [Fact]
    public async Task RunConfiguredAsync_AliasesOverrideConfiguration()
    {
        RunResult result = await RunConfiguredAsync(
        [
            "-t", "alias-tenant",
            "-c", "alias-client",
            "-k", "alias-secret",
            "-d", "",
            "-e", "alias-environment",
            "-s", "alias-schema",
            "-u", "true"
        ],
        CreateValidConfiguration(
            directConnectUrl: "https://example.com/config-direct",
            environmentId: "config-environment",
            schemaName: "config-schema",
            tenantId: "config-tenant",
            useS2SConnection: false,
            appClientId: "config-client",
            appClientSecret: "config-secret"));

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.True(result.TerminalStarted);
        Assert.NotNull(result.Settings);
        Assert.Equal("alias-tenant", result.Settings.TenantId);
        Assert.Equal("alias-client", result.Settings.AppClientId);
        Assert.Equal("alias-secret", result.Settings.AppClientSecret);
        Assert.Null(result.Settings.DirectConnectUrl);
        Assert.Equal("alias-environment", result.Settings.EnvironmentId);
        Assert.Equal("alias-schema", result.Settings.SchemaName);
        Assert.True(result.Settings.UseS2SConnection);
    }

    [Fact]
    public async Task RunConfiguredAsync_OmittedCliValuesUseConfiguration()
    {
        RunResult result = await RunConfiguredAsync(
            ["--layout", "split"],
            CreateValidConfiguration(
                directConnectUrl: "https://example.com/config-direct",
                tenantId: "config-tenant",
                useS2SConnection: true,
                appClientId: "config-client",
                appClientSecret: "config-secret"));

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.True(result.TerminalStarted);
        Assert.NotNull(result.Settings);
        Assert.Equal("config-tenant", result.Settings.TenantId);
        Assert.Equal("config-client", result.Settings.AppClientId);
        Assert.Equal("config-secret", result.Settings.AppClientSecret);
        Assert.Equal("https://example.com/config-direct", result.Settings.DirectConnectUrl);
        Assert.True(result.Settings.UseS2SConnection);
    }

    [Fact]
    public async Task RunConfiguredAsync_ConfiguredSecretSatisfiesCliS2S()
    {
        RunResult result = await RunConfiguredAsync(
            ["--use-s2s-connection", "true"],
            CreateValidConfiguration(
                useS2SConnection: false,
                appClientSecret: "configured-secret"));

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.NotNull(result.Settings);
        Assert.True(result.Settings.UseS2SConnection);
        Assert.Equal("configured-secret", result.Settings.AppClientSecret);
    }

    [Fact]
    public async Task RunConfiguredAsync_CliSecretSatisfiesConfiguredS2S()
    {
        RunResult result = await RunConfiguredAsync(
            ["--app-client-secret", "cli-secret"],
            CreateValidConfiguration(
                useS2SConnection: true,
                appClientSecret: ""));

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.NotNull(result.Settings);
        Assert.True(result.Settings.UseS2SConnection);
        Assert.Equal("cli-secret", result.Settings.AppClientSecret);
    }

    [Fact]
    public async Task RunConfiguredAsync_CliFalseOverridesConfiguredS2SWithoutSecret()
    {
        RunResult result = await RunConfiguredAsync(
            ["--use-s2s-connection", "false"],
            CreateValidConfiguration(
                useS2SConnection: true,
                appClientSecret: ""));

        Assert.True(result.ExitCode == 0, result.Error);
        Assert.NotNull(result.Settings);
        Assert.False(result.Settings.UseS2SConnection);
        Assert.Equal(string.Empty, result.Settings.AppClientSecret);
    }

    [Fact]
    public async Task RunConfiguredAsync_CliWhitespaceOverrideIsStartupErrorWithoutPrintingSecret()
    {
        const string secret = "configured-secret-must-not-be-printed";
        RunResult result = await RunConfiguredAsync(
            ["--tenant-id", " ", "--app-client-id", "\t"],
            CreateValidConfiguration(appClientSecret: secret));

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.TerminalStarted);
        Assert.Null(result.Settings);
        Assert.Contains("Startup failed:", result.Error);
        Assert.Contains("TenantId", result.Error);
        Assert.Contains("AppClientId", result.Error);
        Assert.DoesNotContain(secret, result.Error);
    }

    private static async Task<RunResult> RunConfiguredAsync(
        string[] args,
        IReadOnlyDictionary<string, string?> values)
    {
        HostApplicationBuilder builder = CreateBuilder(values);
        StringWriter error = new();
        SampleConnectionSettings? settings = null;
        bool terminalStarted = false;

        int exitCode = await TerminalProgram.RunConfiguredAsync(
            TerminalOptions.Parse(args),
            builder,
            error,
            (services, _) =>
            {
                settings = services.GetRequiredService<SampleConnectionSettings>();
                terminalStarted = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        return new RunResult(exitCode, settings, terminalStarted, error.ToString());
    }

    private static IReadOnlyDictionary<string, string?> CreateValidConfiguration(
        string directConnectUrl = "https://example.com/direct",
        string environmentId = "",
        string schemaName = "",
        string tenantId = "tenant",
        bool useS2SConnection = false,
        string appClientId = "client",
        string appClientSecret = "")
    {
        return new Dictionary<string, string?>
        {
            ["CopilotStudioClientSettings:DirectConnectUrl"] = directConnectUrl,
            ["CopilotStudioClientSettings:EnvironmentId"] = environmentId,
            ["CopilotStudioClientSettings:SchemaName"] = schemaName,
            ["CopilotStudioClientSettings:TenantId"] = tenantId,
            ["CopilotStudioClientSettings:UseS2SConnection"] = useS2SConnection.ToString(),
            ["CopilotStudioClientSettings:AppClientId"] = appClientId,
            ["CopilotStudioClientSettings:AppClientSecret"] = appClientSecret
        };
    }

    private static HostApplicationBuilder CreateBuilder(
        IReadOnlyDictionary<string, string?> values)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(
            new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(values);
        return builder;
    }

    private sealed record RunResult(
        int ExitCode,
        SampleConnectionSettings? Settings,
        bool TerminalStarted,
        string Error);
}
