// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Agents.Hosting.AgentRunner.Tests;

public class AgentAssemblyLoaderTests
{
    internal static string FixturePath => Path.Combine(AppContext.BaseDirectory, "AgentFixture", "Agent.dll");

    [Fact]
    public void LoadAndRegister_ValidContract_LoadsPrivateDependencyAndSharesSdkTypes()
    {
        var builder = WebApplication.CreateBuilder();
        AgentAssemblyLoader.LoadAndRegister(builder, FixturePath);
        using var services = builder.Services.BuildServiceProvider();

        Assert.NotNull(services.GetRequiredService<AspNetCore.IAgentHttpAdapter>());
        Assert.NotNull(services.GetRequiredService<Builder.IAgent>());
        var marker = builder.Services.Single(service => service.ServiceType.FullName == "RunnerFixture.DependencyMarker");
        Assert.NotNull(services.GetRequiredService(marker.ServiceType));
    }

    [Fact]
    public async Task LoadAndRegister_CustomerJwtConfigurationWithPrivateDependencies_SharesAuthenticationTypes()
    {
        using var key = RSA.Create(2048);
        var signingKey = JsonWebKeyConverter.ConvertFromRSASecurityKey(
            new RsaSecurityKey(key.ExportParameters(false)) { KeyId = "test-key" });
        var metadataPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(signingKey));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.Configuration["AgentRunnerFixtureSigningKeyPath"] = metadataPath;
            AgentAssemblyLoader.LoadAndRegister(builder, FixturePath);
            builder.Services.AddAuthentication().AddJwtBearer("fixture");
            using var services = builder.Services.BuildServiceProvider();

            var options = services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get("fixture");
            var configuration = await options.ConfigurationManager!.GetConfigurationAsync(CancellationToken.None);
            Assert.Equal("test-key", Assert.IsType<JsonWebKey>(Assert.Single(configuration.SigningKeys)).Kid);
        }
        finally
        {
            File.Delete(metadataPath);
        }
    }

    [Fact]
    public void LoadAndRegister_NoGlobalProgram_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            AgentAssemblyLoader.LoadAndRegister(WebApplication.CreateBuilder(), typeof(JsonSerializer).Assembly.Location));
        Assert.Contains("does not define a global-namespace 'Program'", error.Message);
    }

    [Fact]
    public void LoadAndRegister_WrongParameterType_Throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            AgentAssemblyLoader.LoadAndRegister(WebApplication.CreateBuilder(),
                Path.Combine(Path.GetDirectoryName(FixturePath)!, "PrivateDependency.dll")));
        Assert.Contains("public static void RegisterServices(WebApplicationBuilder)", error.Message);
    }

    [Fact]
    public void LoadAndRegister_MissingFile_Throws()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "missing-agent.dll");
        var error = Assert.Throws<FileNotFoundException>(() =>
            AgentAssemblyLoader.LoadAndRegister(WebApplication.CreateBuilder(), path));
        Assert.Equal(path, error.FileName);
    }
}
