#nullable enable

using System;
using System.Collections.Generic;
using CopilotStudioClient.Terminal;
using Microsoft.Extensions.Configuration;

public sealed class SampleConnectionSettingsTests
{
    [Fact]
    public void Constructor_AcceptsDirectConnectionWithoutEnvironmentCoordinates()
    {
        SampleConnectionSettings settings = CreateSettings(
            directConnectUrl: "https://example.com/direct",
            environmentId: "",
            schemaName: "");

        Assert.Equal("https://example.com/direct", settings.DirectConnectUrl);
    }

    [Fact]
    public void Constructor_AcceptsEnvironmentAndSchemaWithoutDirectConnection()
    {
        SampleConnectionSettings settings = CreateSettings(
            directConnectUrl: "",
            environmentId: "environment",
            schemaName: "schema");

        Assert.Equal("environment", settings.EnvironmentId);
        Assert.Equal("schema", settings.SchemaName);
    }

    [Theory]
    [InlineData("AppClientId")]
    [InlineData("TenantId")]
    public void Constructor_RejectsBlankAuthenticationValue(string key)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CreateSettings(overrides: new Dictionary<string, string?>
            {
                [key] = " "
            }));

        Assert.Contains(key, exception.Message);
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData(" ", "environment", "")]
    [InlineData("", "", "schema")]
    public void Constructor_RejectsMissingConnectionCoordinates(
        string directConnectUrl,
        string environmentId,
        string schemaName)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CreateSettings(directConnectUrl, environmentId, schemaName));

        Assert.Contains("DirectConnectUrl", exception.Message);
        Assert.Contains("EnvironmentId", exception.Message);
        Assert.Contains("SchemaName", exception.Message);
    }

    [Fact]
    public void Constructor_S2SRejectsBlankClientSecret()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => CreateSettings(
                useS2SConnection: true,
                overrides: new Dictionary<string, string?>
                {
                    ["AppClientSecret"] = "\t"
                }));

        Assert.Contains("AppClientSecret", exception.Message);
    }

    [Fact]
    public void Constructor_InteractiveAuthenticationDoesNotRequireClientSecret()
    {
        SampleConnectionSettings settings = CreateSettings(
            useS2SConnection: false,
            overrides: new Dictionary<string, string?>
            {
                ["AppClientSecret"] = ""
            });

        Assert.False(settings.UseS2SConnection);
    }

    private static SampleConnectionSettings CreateSettings(
        string directConnectUrl = "https://example.com/direct",
        string environmentId = "",
        string schemaName = "",
        bool useS2SConnection = false,
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> values = new()
        {
            ["CopilotStudioClientSettings:DirectConnectUrl"] = directConnectUrl,
            ["CopilotStudioClientSettings:EnvironmentId"] = environmentId,
            ["CopilotStudioClientSettings:SchemaName"] = schemaName,
            ["CopilotStudioClientSettings:TenantId"] = "tenant",
            ["CopilotStudioClientSettings:UseS2SConnection"] = useS2SConnection.ToString(),
            ["CopilotStudioClientSettings:AppClientId"] = "client",
            ["CopilotStudioClientSettings:AppClientSecret"] = "secret"
        };

        if (overrides is not null)
        {
            foreach ((string key, string? value) in overrides)
            {
                values[$"CopilotStudioClientSettings:{key}"] = value;
            }
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        return new SampleConnectionSettings(
            configuration.GetSection("CopilotStudioClientSettings"));
    }
}
