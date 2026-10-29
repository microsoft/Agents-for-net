// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Agents.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;

namespace Microsoft.Agents.Hosting.AgentRunner;

internal static class AgentRunnerAuthentication
{
    internal const string BotFrameworkScheme = "AgentRunnerBearer.BotFramework";
    internal const string EntraScheme = "AgentRunnerBearer.Entra";
    private const string Scheme = "AgentRunnerBearer";
    private static readonly JsonWebTokenHandler TokenHandler = new();

    internal static void AddTokenValidation(IServiceCollection services, IConfiguration configuration)
    {
        var clientId = configuration["Connections:ServiceConnection:Settings:ClientId"];
        var tenantId = configuration["Connections:ServiceConnection:Settings:TenantId"];
        if (!Guid.TryParse(clientId, out _))
        {
            throw new InvalidOperationException(
                "Configuration 'Connections:ServiceConnection:Settings:ClientId' must contain a valid GUID.");
        }

        if (!Guid.TryParse(tenantId, out _))
        {
            throw new InvalidOperationException(
                "Configuration 'Connections:ServiceConnection:Settings:TenantId' must contain a valid GUID.");
        }

        var issuers = new List<string>
        {
            string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, tenantId),
            string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV2, tenantId),
        };
        foreach (var child in configuration.GetSection("AgentRunner:Authentication:AdditionalEntraIssuers").GetChildren())
        {
            if (!Uri.TryCreate(child.Value, UriKind.Absolute, out var issuer) ||
                issuer.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException(
                    $"Configuration '{child.Path}' must contain an absolute HTTPS issuer URI.");
            }

            issuers.Add(child.Value!);
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = Scheme;
            options.DefaultChallengeScheme = Scheme;
        })
        .AddPolicyScheme(Scheme, null, options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                var parts = context.Request.Headers.Authorization.ToString()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
                    !TokenHandler.CanReadToken(parts[1]))
                {
                    return EntraScheme;
                }

                // The unvalidated issuer selects a validator; it never grants access.
                return new JsonWebToken(parts[1]).Issuer == AuthenticationConstants.BotFrameworkTokenIssuer
                    ? BotFrameworkScheme : EntraScheme;
            };
        })
        .AddJwtBearer(BotFrameworkScheme, options => Configure(
            options, clientId, [AuthenticationConstants.BotFrameworkTokenIssuer],
            AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl))
        .AddJwtBearer(EntraScheme, options => Configure(
            options, clientId, issuers, AuthenticationConstants.PublicOpenIdMetadataUrl));
    }

    private static void Configure(
        JwtBearerOptions options, string clientId, IEnumerable<string> issuers, string metadataAddress)
    {
        options.SaveToken = true;
        options.MetadataAddress = metadataAddress;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = issuers,
            ValidateAudience = true,
            ValidAudience = clientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
        };
        options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();
    }
}
