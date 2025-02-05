// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Protocols;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Validators;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.Setup
{
    public static class AspNetExtensions
    {
        private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _openIdMetadataCache =
            new ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>>();

        /// <summary>
        /// Adds token validation typical for ABS/SMBA and Bot-to-bot.
        /// default to Azure Public Cloud.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="authenticationSection">Name of the config section to read.</param>
        /// <param name="logger">Optional logger to use for authentication event logging.</param>
        /// <remarks>
        /// Example config:
        /// <code>
        ///     {
        ///        "TokenValidation": {
        ///           "Audience": "{required:bot-appid},
        ///           "TenantId": "{recommended:tenant-id}",
        ///           "ValidIssuers": [
        ///              "{default:Public-AzureBotService}"
        ///           ],
        ///           "IsGov": {optional:false},
        ///           "AzureBotServiceOpenIdMetadataUrl": optional,
        ///           "OpenIdMetadataUrl": optional,
        ///           "AzureBotServiceTokenHandling": "{optional:true}"
        ///        }
        ///     }
        /// </code>
        /// 
        /// `IsGov` can be omitted, in which case public Azure Bot Service and Azure Cloud metadata urls are used.
        /// `ValidIssuers` can be omitted, in which case the Public Azure Bot Service issuers are used.
        /// `TenantId` can be omitted if the Agent is not being called by another Agent.  Otherwise it is used to add other known issuers.  Only when `ValidIssuers` is omitted.
        /// `AzureBotServiceOpenIdMetadataUrl` can be omitted.  In which case default values in combination with `IsGov` is used.
        /// `OpenIdMetadataUrl` can be omitted.  In which case default values in combination with `IsGov` is used.
        /// `AzureBotServiceTokenHandling` defaults to true and should always be true until Azure Bot Service sends Entra ID token.
        /// </remarks>
        public static void AddBotAspNetAuthentication(this IServiceCollection services, IConfiguration configuration, string authenticationSection = "TokenValidation", ILogger logger = null)
        {
            IConfigurationSection tokenValidationSection = configuration.GetSection("TokenValidation");

            List<string> validTokenIssuers = tokenValidationSection.GetSection("ValidIssuers").Get<List<string>>();

            // If ValidIssuers is empty, default for ABS Public Cloud
            if (validTokenIssuers == null || validTokenIssuers.Count == 0)
            {
                validTokenIssuers =
                [
                    "https://api.botframework.com",
                    "https://sts.windows.net/d6d49420-f39b-4df7-a1dc-d59a935871db/",
                    "https://login.microsoftonline.com/d6d49420-f39b-4df7-a1dc-d59a935871db/v2.0",
                    "https://sts.windows.net/f8cdef31-a31e-4b4a-93e4-5f571e91255a/",
                    "https://login.microsoftonline.com/f8cdef31-a31e-4b4a-93e4-5f571e91255a/v2.0",
                ];

                string tenantId = tokenValidationSection["TenantId"];
                if (!string.IsNullOrEmpty(tenantId))
                {
                    validTokenIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV1, tenantId));
                    validTokenIssuers.Add(string.Format(CultureInfo.InvariantCulture, AuthenticationConstants.ValidTokenIssuerUrlTemplateV2, tenantId));
                }
            }

            string audience = tokenValidationSection["Audience"];
            if (string.IsNullOrEmpty(audience))
            {
                throw new ArgumentNullException(audience, $"{authenticationSection}:Audience not set");
            }

            bool isGov = tokenValidationSection.GetValue<bool>("IsGov", false);
            var azureBotServiceTokenHandling = tokenValidationSection.GetValue<bool>("AzureBotServiceTokenHandling", true);

            // If the `AzureBotServiceOpenIdMetadataUrl` setting is not specified, use the default based on `IsGov`.  This is what is used to authenticate ABS tokens.
            var azureBotServiceOpenIdMetadataUrl = tokenValidationSection["AzureBotServiceOpenIdMetadataUrl"];
            if (string.IsNullOrEmpty(azureBotServiceOpenIdMetadataUrl))
            {
                azureBotServiceOpenIdMetadataUrl = isGov ? AuthenticationConstants.GovAzureBotServiceOpenIdMetadataUrl : AuthenticationConstants.PublicAzureBotServiceOpenIdMetadataUrl;
            }

            // If the `OpenIdMetadataUrl` setting is not specified, use the default based on `IsGov`.  This is what is used to authenticate Entra ID tokens.
            var openIdMetadataUrl = tokenValidationSection["OpenIdMetadataUrl"];
            if (string.IsNullOrEmpty(openIdMetadataUrl))
            {
                openIdMetadataUrl = isGov ? AuthenticationConstants.GovOpenIdMetadataUrl : AuthenticationConstants.PublicOpenIdMetadataUrl;
            }

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    ValidIssuers = validTokenIssuers,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                };

                // Using Microsoft.IdentityModel.Validators
                options.TokenValidationParameters.EnableAadSigningKeyIssuerValidation();

                options.Events = new JwtBearerEvents
                {
                    // Create a ConfigurationManager based on the requestor.  This is to handle ABS non-Entra tokens.
                    OnMessageReceived = async context =>
                    {
                        var authorizationHeader = context.Request.Headers.Authorization.ToString();

                        if (string.IsNullOrEmpty(authorizationHeader))
                        {
                            // Default to AadTokenValidation handling
                            context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                            await Task.CompletedTask.ConfigureAwait(false);
                            return;
                        }

                        string[] parts = authorizationHeader?.Split(' ');
                        if (parts.Length != 2 || parts[0] != "Bearer")
                        {
                            // Default to AadTokenValidation handling
                            context.Options.TokenValidationParameters.ConfigurationManager ??= options.ConfigurationManager as BaseConfigurationManager;
                            await Task.CompletedTask.ConfigureAwait(false);
                            return;
                        }

                        JwtSecurityToken token = new(parts[1]);
                        var issuer = token.Claims.FirstOrDefault(claim => claim.Type == AuthenticationConstants.IssuerClaim)?.Value;

                        if (azureBotServiceTokenHandling && AuthenticationConstants.BotFrameworkTokenIssuer.Equals(issuer))
                        {
                            // Use the Bot Framework authority for this configuration manager
                            context.Options.TokenValidationParameters.ConfigurationManager = _openIdMetadataCache.GetOrAdd(azureBotServiceOpenIdMetadataUrl, key =>
                            {
                                return new ConfigurationManager<OpenIdConnectConfiguration>(azureBotServiceOpenIdMetadataUrl, new OpenIdConnectConfigurationRetriever(), new HttpClient());
                            });
                        }
                        else
                        {
                            context.Options.TokenValidationParameters.ConfigurationManager = _openIdMetadataCache.GetOrAdd(openIdMetadataUrl, key =>
                            {
                                return new ConfigurationManager<OpenIdConnectConfiguration>(openIdMetadataUrl, new OpenIdConnectConfigurationRetriever(), new HttpClient());
                            });
                        }

                        await Task.CompletedTask.ConfigureAwait(false);
                    },

                    OnTokenValidated = context =>
                    {
                        logger?.LogDebug("TOKEN Validated");
                        return Task.CompletedTask;
                    },
                    OnForbidden = context =>
                    {
                        logger?.LogWarning(context.Result.ToString());
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        logger?.LogWarning(context.Exception.ToString());
                        return Task.CompletedTask;
                    }
                };
            });
        }
    }
}
