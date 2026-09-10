extern alias A2AAgentSample;

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using A2AAgentSample::A2AAgent;

namespace Microsoft.Agents.Samples.A2A.Tests;

/// <summary>
/// Verifies the sample's authentication startup decision and the identity shape that ASP.NET Core JWT
/// bearer validation actually produces. No live tenant is required: tokens are signed with a local test
/// key and validated by the very <see cref="JsonWebTokenHandler"/> instance the sample's
/// <see cref="JwtBearerOptions"/> configures.
/// </summary>
public class A2AAgentStartupTests
{
    private const string TestTenantId = "11111111-1111-1111-1111-111111111111";
    private const string TestAudience = "22222222-2222-2222-2222-222222222222";
    private const string TestClientId = "33333333-3333-3333-3333-333333333333";
    private const string TestIssuer = "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111/v2.0";

    [Fact]
    public async Task Development_WithConfiguredTokenValidation_RegistersJwtBearer()
    {
        using WebApplication app = BuildApp(Environments.Development, configureTokenValidation: true);

        var schemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.NotNull(await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme));
        Assert.True(((IEndpointRouteBuilder)app).IsAgentAuthorizationConfigured());
    }

    [Fact]
    public async Task Development_WithPlaceholderTokenValidation_StaysAnonymous()
    {
        using WebApplication app = BuildApp(Environments.Development, configureTokenValidation: false);

        var schemeProvider = app.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Null(await schemeProvider.GetSchemeAsync(JwtBearerDefaults.AuthenticationScheme));
        Assert.False(((IEndpointRouteBuilder)app).IsAgentAuthorizationConfigured());
    }

    [Fact]
    public void Production_WithPlaceholderTokenValidation_StillRequiresTokenValidation()
    {
        IConfiguration configuration = CreateConfiguration(configureTokenValidation: false);

        Assert.True(A2AAgentStartup.ShouldEnableTokenValidation(configuration, new StubEnvironment(Environments.Production)));
        Assert.False(A2AAgentStartup.ShouldEnableTokenValidation(configuration, new StubEnvironment(Environments.Development)));
    }

    [Fact]
    public void A2AEndpoints_RemainAnonymous_WhenTokenValidationIsEnabled()
    {
        using WebApplication app = BuildApp(Environments.Development, configureTokenValidation: true);
        A2AAgentStartup.ConfigureApplication(app);

        var a2aEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.Contains("a2a", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        Assert.NotEmpty(a2aEndpoints);
        Assert.All(a2aEndpoints, endpoint => Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>()));
    }

    [Fact]
    public async Task JwtBearerConfiguration_MapsInboundClaims()
    {
        JsonWebTokenHandler handler = GetConfiguredTokenHandler(out _);

        Assert.True(handler.MapInboundClaims);

        ClaimsIdentity identity = await CreateValidatedIdentityAsync(DelegatedClaims());

        // Documents the mapping the sample must tolerate: short Entra names are renamed on the identity.
        Assert.Null(identity.FindFirst("tid"));
        Assert.Equal(TestTenantId, identity.FindFirst(A2ATokenIdentity.MappedTenantIdClaim)?.Value);
        Assert.Null(identity.FindFirst("scp"));
        Assert.NotNull(identity.FindFirst(A2ATokenIdentity.MappedScopeClaim));
    }

    [Fact]
    public async Task DelegatedIdentity_FromJwtBearer_IsAcceptedAndSummarized()
    {
        ClaimsIdentity identity = await CreateValidatedIdentityAsync(DelegatedClaims());

        A2ATokenIdentity.RequireDelegated(identity);
        Assert.Throws<InvalidOperationException>(() => A2ATokenIdentity.RequireApplication(identity));

        Assert.Equal(TestTenantId, A2ATokenIdentity.FindTenantId(identity));
        Assert.Equal("user-object-id", A2ATokenIdentity.FindObjectId(identity));
        Assert.Equal("user-subject", A2ATokenIdentity.FindSubject(identity));
    }

    [Fact]
    public async Task ApplicationIdentity_FromJwtBearer_IsAcceptedAndSummarized()
    {
        ClaimsIdentity identity = await CreateValidatedIdentityAsync(ApplicationClaims());

        A2ATokenIdentity.RequireApplication(identity);
        Assert.Throws<InvalidOperationException>(() => A2ATokenIdentity.RequireDelegated(identity));

        Assert.Equal(TestTenantId, A2ATokenIdentity.FindTenantId(identity));
        Assert.Equal("app-subject", A2ATokenIdentity.FindSubject(identity));
        Assert.Equal(TestClientId, A2ATokenIdentity.FindApplicationId(identity));
        Assert.True(A2ATokenIdentity.HasRoles(identity));
    }

    [Fact]
    public async Task ApplicationRolesIdentity_WithoutIdentityTypeClaim_IsStillApplication()
    {
        Dictionary<string, object> claims = ApplicationClaims();
        claims.Remove(A2ATokenIdentity.IdentityTypeClaim);

        ClaimsIdentity identity = await CreateValidatedIdentityAsync(claims);

        A2ATokenIdentity.RequireApplication(identity);
    }

    private static Dictionary<string, object> DelegatedClaims() => new()
    {
        ["tid"] = TestTenantId,
        ["oid"] = "user-object-id",
        ["sub"] = "user-subject",
        ["scp"] = "access_as_user",
        ["azp"] = TestClientId,
    };

    private static Dictionary<string, object> ApplicationClaims() => new()
    {
        ["tid"] = TestTenantId,
        ["sub"] = "app-subject",
        [A2ATokenIdentity.IdentityTypeClaim] = "app",
        ["roles"] = new[] { "A2A.Access" },
        ["azp"] = TestClientId,
    };

    private static async Task<ClaimsIdentity> CreateValidatedIdentityAsync(Dictionary<string, object> claims)
    {
        JsonWebTokenHandler handler = GetConfiguredTokenHandler(out SecurityKey signingKey);

        string token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = TestIssuer,
            Audience = TestAudience,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = claims,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
        });

        TokenValidationResult result = await handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = TestIssuer,
            ValidAudience = TestAudience,
            IssuerSigningKey = signingKey,
        });

        Assert.True(result.IsValid);
        return result.ClaimsIdentity;
    }

    /// <summary>
    /// Returns the token handler from the <see cref="JwtBearerOptions"/> the sample startup registers, so
    /// claim-shape assertions reflect the real configuration instead of a hand-built identity.
    /// </summary>
    private static JsonWebTokenHandler GetConfiguredTokenHandler(out SecurityKey signingKey)
    {
        using WebApplication app = BuildApp(Environments.Development, configureTokenValidation: true);

        JwtBearerOptions options = app.Services
            .GetRequiredService<global::Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        signingKey = new SymmetricSecurityKey(SHA256.HashData("a2a-agent-startup-tests"u8.ToArray()));
        return options.TokenHandlers.OfType<JsonWebTokenHandler>().Single();
    }

    private static WebApplication BuildApp(string environmentName, bool configureTokenValidation)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
        });

        builder.Configuration.AddInMemoryCollection(CreateSettings(configureTokenValidation));
        A2AAgentStartup.ConfigureBuilder(builder);
        return builder.Build();
    }

    private static IConfiguration CreateConfiguration(bool configureTokenValidation)
        => new ConfigurationBuilder().AddInMemoryCollection(CreateSettings(configureTokenValidation)).Build();

    private static Dictionary<string, string?> CreateSettings(bool configureTokenValidation) => new()
    {
        ["TokenValidation:Audiences:0"] = configureTokenValidation ? TestAudience : "{{ClientId}}",
        ["TokenValidation:TenantId"] = configureTokenValidation ? TestTenantId : "{{TenantId}}",
    };

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "A2AAgent";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public global::Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new global::Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
