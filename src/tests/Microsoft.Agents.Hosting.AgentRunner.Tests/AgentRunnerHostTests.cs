// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Agents.Hosting.AgentRunner.Tests;

[CollectionDefinition("AgentRunnerEnvironment", DisableParallelization = true)]
public class AgentRunnerEnvironmentCollection;

[Collection("AgentRunnerEnvironment")]
public class AgentRunnerHostTests
{
    private const string ClientId = "11111111-1111-1111-1111-111111111111";
    private const string TenantIssuer = "https://login.microsoftonline.com/22222222-2222-2222-2222-222222222222/v2.0";
    private const string ExtraIssuer = "https://login.microsoftonline.com/33333333-3333-3333-3333-333333333333/v2.0";

    [Fact]
    public async Task BuildApp_ValidContract_UsesProductionAndLoopback()
    {
        await using var app = AgentRunnerHost.BuildApp(new(3978, AgentAssemblyLoaderTests.FixturePath));
        Assert.Equal("Production", app.Environment.EnvironmentName);
        Assert.Equal(["http://127.0.0.1:3978"], app.Urls);
        Assert.Equal("from-customer", app.Configuration["AgentRunnerFixtureValue"]);
        var options = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        Assert.NotSame(options.Get(AgentRunnerAuthentication.BotFrameworkScheme).ConfigurationManager,
            options.Get(AgentRunnerAuthentication.EntraScheme).ConfigurationManager);
        Assert.Equal([
            "https://sts.windows.net/22222222-2222-2222-2222-222222222222/", TenantIssuer],
            options.Get(AgentRunnerAuthentication.EntraScheme).TokenValidationParameters.ValidIssuers);
    }

    [Fact]
    public async Task BuildApp_EnvironmentOverride_TakesPrecedenceOverCustomerSettings()
    {
        var previous = Environment.GetEnvironmentVariable("AgentRunnerFixtureValue");
        try
        {
            Environment.SetEnvironmentVariable("AgentRunnerFixtureValue", "from-host");
            await using var app = AgentRunnerHost.BuildApp(new(3978, AgentAssemblyLoaderTests.FixturePath));
            Assert.Equal("from-host", app.Configuration["AgentRunnerFixtureValue"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AgentRunnerFixtureValue", previous);
        }
    }

    [Theory]
    [InlineData("Connections:ServiceConnection:Settings:ClientId", null)]
    [InlineData("Connections:ServiceConnection:Settings:ClientId", "not-a-guid")]
    [InlineData("Connections:ServiceConnection:Settings:TenantId", null)]
    [InlineData("Connections:ServiceConnection:Settings:TenantId", "not-a-guid")]
    [InlineData("AgentRunner:Authentication:AdditionalEntraIssuers:0", "http://insecure.example")]
    public void AddTokenValidation_InvalidIdentityOrIssuer_FailsClosed(string key, string? value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Connections:ServiceConnection:Settings:ClientId"] = ClientId,
            ["Connections:ServiceConnection:Settings:TenantId"] = "22222222-2222-2222-2222-222222222222",
        };
        values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var error = Assert.Throws<InvalidOperationException>(() =>
            AgentRunnerAuthentication.AddTokenValidation(new ServiceCollection(), configuration));
        Assert.Contains(key, error.Message);
    }

    [Fact]
    public async Task Messages_SignedBotAndEntraTokens_DispatchConcurrently()
    {
        using var key = RSA.Create(2048);
        await using var app = CreateHost(key);
        await app.StartAsync();
        using var client = CreateClient(app);
        var tokens = new[]
        {
            CreateToken(key, AuthenticationConstants.BotFrameworkTokenIssuer),
            CreateToken(key, TenantIssuer),
        };
        var responses = await Task.WhenAll(tokens.Select(token => SendAsync(client, token)));
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
                Assert.Equal("private dependency reached", await response.Content.ReadAsStringAsync());
            }
        }
        await app.StopAsync();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("unsigned")]
    [InlineData("wrong-audience")]
    [InlineData("wrong-tenant")]
    [InlineData("expired")]
    [InlineData("wrong-key")]
    public async Task Messages_InvalidToken_IsUnauthorized(string scenario)
    {
        using var key = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        await using var app = CreateHost(key);
        await app.StartAsync();
        using var client = CreateClient(app);
        var token = scenario switch
        {
            "missing" => null,
            "malformed" => "not-a-token",
            "unsigned" => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = TenantIssuer, Audience = ClientId,
            }),
            "wrong-audience" => CreateToken(key, TenantIssuer, "44444444-4444-4444-4444-444444444444"),
            "wrong-tenant" => CreateToken(key, ExtraIssuer),
            "expired" => CreateToken(key, TenantIssuer, expired: true),
            "wrong-key" => CreateToken(otherKey, TenantIssuer),
            _ => throw new InvalidOperationException(),
        };
        using var response = await SendAsync(client, token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await app.StopAsync();
    }

    [Fact]
    public async Task Messages_ExplicitAdditionalIssuer_IsAccepted()
    {
        const string variable = "AgentRunner__Authentication__AdditionalEntraIssuers__0";
        var previous = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, ExtraIssuer);
            using var key = RSA.Create(2048);
            await using var app = CreateHost(key);
            await app.StartAsync();
            using var client = CreateClient(app);
            using var response = await SendAsync(client, CreateToken(key, ExtraIssuer));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            await app.StopAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, previous);
        }
    }

    private static WebApplication CreateHost(RSA rsa)
    {
        var app = AgentRunnerHost.BuildApp(new(0, AgentAssemblyLoaderTests.FixturePath));
        var options = app.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        foreach (var scheme in new[] { AgentRunnerAuthentication.BotFrameworkScheme, AgentRunnerAuthentication.EntraScheme })
        {
            var configuration = new OpenIdConnectConfiguration();
            configuration.SigningKeys.Add(new RsaSecurityKey(rsa) { KeyId = "test-key" });
            options.Get(scheme).ConfigurationManager =
                new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
        }
        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        return new HttpClient { BaseAddress = new Uri(addresses.Addresses.Single()) };
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/messages");
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return SendAndDisposeAsync(client, request);
    }

    private static async Task<HttpResponseMessage> SendAndDisposeAsync(HttpClient client, HttpRequestMessage request)
    {
        using (request)
        {
            return await client.SendAsync(request).ConfigureAwait(false);
        }
    }

    private static string CreateToken(RSA key, string issuer, string audience = ClientId, bool expired = false)
    {
        var now = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = now.AddHours(-2),
            NotBefore = now.AddHours(-2),
            Expires = expired ? now.AddHours(-1) : now.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(key) { KeyId = "test-key" }, SecurityAlgorithms.RsaSha256),
        });
    }
}
