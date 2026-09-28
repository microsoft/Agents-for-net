// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable
extern alias AuthenticationSample;

using SampleAspNetExtensions = AuthenticationSample::AspNetExtensions;

using Microsoft.Agents.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.SampleTest
{
    public class AspNetAuthenticationTests
    {
        private const string Tenant = "11111111-2222-4333-8444-555555555555";
        private const string OtherTenant = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        private const string OtherCaller = "22222222-3333-4444-8555-666666666666";
        private const string Audience = "12345678-1234-4234-8234-123456789abc";
        private const string Caller = "87654321-4321-4321-8321-cba987654321";

        [Fact]
        public void StrictModeKeepsStaticIssuerValidation()
        {
            var options = CreateBearerOptions();
            Assert.True(options.TokenValidationParameters.ValidateIssuer);
            Assert.Null(options.TokenValidationParameters.IssuerValidator);
            Assert.Contains(AuthenticationConstants.BotFrameworkTokenIssuer, options.TokenValidationParameters.ValidIssuers);
            Assert.DoesNotContain(PublicIssuer(Tenant), options.TokenValidationParameters.ValidIssuers);
        }

        [Fact]
        public async Task StrictModeRejectsRuntimeIssuerButAcceptsConfiguredIssuer()
        {
            var key = new SymmetricSecurityKey(new byte[32]);
            var handler = new JsonWebTokenHandler();
            var token = CreateSignedToken(handler, key, PublicIssuer(Tenant), Audience, DateTime.UtcNow.AddMinutes(30));

            var strict = CreateBearerOptions().TokenValidationParameters.Clone();
            strict.IssuerSigningKey = key;
            Assert.False((await handler.ValidateTokenAsync(token, strict)).IsValid);

            var listed = CreateBearerOptions(validIssuers: [PublicIssuer(Tenant)]).TokenValidationParameters.Clone();
            listed.IssuerSigningKey = key;
            Assert.True((await handler.ValidateTokenAsync(token, listed)).IsValid);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DynamicModeAcceptsCanonicalIssuerWithMatchingTenant(bool isGov)
        {
            var options = CreateBearerOptions(dynamic: true, isGov: isGov);
            var issuer = isGov ? GovIssuer(Tenant) : PublicIssuer(Tenant);
            Assert.Equal(issuer, ValidateIssuer(options, issuer, Tenant));
            Assert.Equal($"https://sts.windows.net/{Tenant}/", ValidateIssuer(options, $"https://sts.windows.net/{Tenant}/", Tenant));
            Assert.Equal(issuer, ValidateIssuer(options, issuer, Tenant.ToUpperInvariant()));
        }

        [Theory]
        [InlineData("https://login.microsoftonline.com/common/v2.0")]
        [InlineData("https://login.microsoftonline.com/organizations/v2.0")]
        [InlineData("https://evil.example/11111111-2222-4333-8444-555555555555/v2.0")]
        [InlineData("https://login.microsoftonline.com.evil.example/11111111-2222-4333-8444-555555555555/v2.0")]
        [InlineData("https://login.microsoftonline.com:444/11111111-2222-4333-8444-555555555555/v2.0")]
        [InlineData("https://login.microsoftonline.com/11111111-2222-4333-8444-555555555555/v2.0/")]
        [InlineData("https://login.microsoftonline.com/11111111-2222-4333-8444-555555555555/extra/v2.0")]
        [InlineData("https://sts.windows.net/11111111-2222-4333-8444-555555555555/v2.0")]
        [InlineData("https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0")]
        [InlineData("http://login.microsoftonline.com/11111111-2222-4333-8444-555555555555/v2.0")]
        [InlineData("https://login.microsoftonline.com/11111111-2222-4333-8444-555555555555/v2.0?x=1")]
        [InlineData("https://LOGIN.microsoftonline.com/11111111-2222-4333-8444-555555555555/v2.0")]
        [InlineData("https://login.microsoftonline.com/11111111-2222-4333-8444-555555555555/V2.0")]
        public void DynamicModeRejectsNonCanonicalIssuers(string issuer)
        {
            var options = CreateBearerOptions(dynamic: true);
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(options, issuer, Tenant));
        }

        [Fact]
        public void DynamicModeRejectsMissingMismatchedAndDuplicateTenant()
        {
            var options = CreateBearerOptions(dynamic: true);
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(options, PublicIssuer(Tenant), OtherTenant));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(options, PublicIssuer(Tenant), "not-a-guid"));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(options, PublicIssuer(Tenant)));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(options, PublicIssuer(Tenant), Tenant, Tenant));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(options, null, Tenant));
        }

        [Fact]
        public void DynamicModeRejectsCrossCloudIssuers()
        {
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(CreateBearerOptions(dynamic: true), GovIssuer(Tenant), Tenant));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(CreateBearerOptions(dynamic: true, isGov: true), PublicIssuer(Tenant), Tenant));
        }

        [Fact]
        public void V1IssuerIsSharedByPublicAndGovernmentClouds()
        {
            var issuer = $"https://sts.windows.net/{Tenant}/";
            Assert.Equal(issuer, ValidateIssuer(CreateBearerOptions(dynamic: true), issuer, Tenant));
            Assert.Equal(issuer, ValidateIssuer(CreateBearerOptions(dynamic: true, isGov: true), issuer, Tenant));
        }

        [Fact]
        public void DynamicModeAlsoValidatesJwtSecurityTokenClaims()
        {
            var options = CreateBearerOptions(dynamic: true);
            var issuer = PublicIssuer(Tenant);
            var token = new JwtSecurityToken(issuer: issuer, claims: [new Claim("tid", Tenant)]);
            Assert.Equal(issuer, options.TokenValidationParameters.IssuerValidator!(issuer, token, options.TokenValidationParameters));

            var mismatch = new JwtSecurityToken(issuer: issuer, claims: [new Claim("tid", OtherTenant)]);
            Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
                options.TokenValidationParameters.IssuerValidator!(issuer, mismatch, options.TokenValidationParameters));
        }

        [Fact]
        public void DynamicModeKeepsExplicitAllowListAndBotServicePolicy()
        {
            var options = CreateBearerOptions(dynamic: true, validIssuers: ["https://custom.example/issuer", AuthenticationConstants.BotFrameworkTokenIssuer]);
            Assert.Equal("https://custom.example/issuer", ValidateIssuer(options, "https://custom.example/issuer"));
            Assert.Equal(AuthenticationConstants.BotFrameworkTokenIssuer, ValidateIssuer(options, AuthenticationConstants.BotFrameworkTokenIssuer));

            var crossCloudOptions = CreateBearerOptions(dynamic: true, validIssuers:
            [
                AuthenticationConstants.GovBotFrameworkTokenIssuer,
                AuthenticationConstants.ChinaBotFrameworkTokenIssuer
            ]);
            Assert.Equal(AuthenticationConstants.GovBotFrameworkTokenIssuer, ValidateIssuer(crossCloudOptions, AuthenticationConstants.GovBotFrameworkTokenIssuer));
            Assert.Equal(AuthenticationConstants.ChinaBotFrameworkTokenIssuer, ValidateIssuer(crossCloudOptions, AuthenticationConstants.ChinaBotFrameworkTokenIssuer));

            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(
                CreateBearerOptions(dynamic: true, handleBotService: false, validIssuers: [AuthenticationConstants.BotFrameworkTokenIssuer]),
                AuthenticationConstants.BotFrameworkTokenIssuer));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(
                CreateBearerOptions(dynamic: true, handleBotService: false, validIssuers: [AuthenticationConstants.GovBotFrameworkTokenIssuer]),
                AuthenticationConstants.GovBotFrameworkTokenIssuer));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(CreateBearerOptions(dynamic: true, isGov: true), AuthenticationConstants.BotFrameworkTokenIssuer));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(CreateBearerOptions(dynamic: true), AuthenticationConstants.GovBotFrameworkTokenIssuer));
            Assert.Throws<SecurityTokenInvalidIssuerException>(() => ValidateIssuer(options, AuthenticationConstants.BotFrameworkTokenIssuer.ToUpperInvariant()));
        }

        [Fact]
        public async Task DynamicModeRetainsConfiguredIssuerAndMappedTenantBehavior()
        {
            var issuer = PublicIssuer(Tenant);
            var options = CreateBearerOptions(dynamic: true, validIssuers: [issuer]);
            Assert.Equal(issuer, ValidateIssuer(options, issuer));
            Assert.Equal(issuer, ValidateIssuer(options, issuer, Tenant, Tenant));

            var token = new JwtSecurityToken(issuer: issuer, claims:
            [
                new Claim("http://schemas.microsoft.com/identity/claims/tenantid", Tenant),
                new Claim("azp", Caller)
            ]);
            Assert.Equal(issuer, options.TokenValidationParameters.IssuerValidator!(issuer, token, options.TokenValidationParameters));

            var context = CreateValidatedContext(options, Caller);
            context.Principal = new ClaimsPrincipal(new ClaimsIdentity(token.Claims, JwtBearerDefaults.AuthenticationScheme));
            await options.Events.OnTokenValidated(context);
            Assert.Null(context.Result?.Failure);

            var runtimeOnly = CreateBearerOptions(dynamic: true);
            Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
                runtimeOnly.TokenValidationParameters.IssuerValidator!(issuer, token, runtimeOnly.TokenValidationParameters));
        }

        [Fact]
        public void DynamicModeRetainsDefaultBotServiceEntraIssuers()
        {
            var options = CreateBearerOptions(dynamic: true);
            foreach (var tenant in new[]
            {
                "d6d49420-f39b-4df7-a1dc-d59a935871db",
                "f8cdef31-a31e-4b4a-93e4-5f571e91255a",
                "69e9b82d-4842-4902-8d1e-abc5b98a55e8"
            })
            {
                foreach (var issuer in new[] { PublicIssuer(tenant), $"https://sts.windows.net/{tenant}/" })
                {
                    Assert.Contains(issuer, options.TokenValidationParameters.ValidIssuers);
                    Assert.Equal(issuer, ValidateIssuer(options, issuer));
                }
            }
        }

        [Fact]
        public void DynamicModeRejectsUnrestrictedCallersAndConflictingOptionsAtStartup()
        {
            foreach (IList<string>? callers in new IList<string>?[] { null, [], ["*"], [Caller, "*"], [" "] })
            {
                var settings = CreateSettings(dynamic: true);
                settings.AllowedCallers = callers;
                Assert.Throws<ArgumentException>(() => new ServiceCollection().AddAgentAspNetAuthentication(settings));
            }

            var botServiceOnly = CreateSettings(dynamic: true);
            botServiceOnly.AzureBotServiceOnly = true;
            Assert.Throws<ArgumentException>(() => new ServiceCollection().AddAgentAspNetAuthentication(botServiceOnly));

            // The new requirements apply only to the opt-in mode.
            var strict = CreateSettings();
            strict.AllowedCallers = null;
            new ServiceCollection().AddAgentAspNetAuthentication(strict);
        }

        [Fact]
        public void DynamicModeRequiresMatchingMetadataCloud()
        {
            var settings = CreateSettings(dynamic: true);
            settings.OpenIdMetadataUrl = AuthenticationConstants.GovOpenIdMetadataUrl;
            Assert.Throws<ArgumentException>(() => new ServiceCollection().AddAgentAspNetAuthentication(settings));

            settings.OpenIdMetadataUrl = "https://example.com/common/v2.0/.well-known/openid-configuration";
            Assert.Throws<ArgumentException>(() => new ServiceCollection().AddAgentAspNetAuthentication(settings));
        }

        [Fact]
        public void DynamicModeRetainsOtherTokenValidationRequirements()
        {
            var parameters = CreateBearerOptions(dynamic: true).TokenValidationParameters;
            Assert.True(parameters.ValidateIssuer);
            Assert.True(parameters.ValidateAudience);
            Assert.True(parameters.ValidateLifetime);
            Assert.True(parameters.ValidateIssuerSigningKey);
            Assert.True(parameters.RequireSignedTokens);
            Assert.NotNull(parameters.IssuerSigningKeyValidatorUsingConfiguration);
            Assert.Contains(Audience, parameters.ValidAudiences);
        }

        [Fact]
        public async Task DynamicModeRetainsAllowedCallersCheck()
        {
            var options = CreateBearerOptions(dynamic: true);
            var denied = CreateValidatedContext(options, OtherCaller);
            await options.Events.OnTokenValidated(denied);
            Assert.NotNull(denied.Result?.Failure);

            var allowed = CreateValidatedContext(options, Caller);
            await options.Events.OnTokenValidated(allowed);
            Assert.Null(allowed.Result?.Failure);
        }

        [Fact]
        public async Task BearerHandlerRunsMessageIssuerAndCallerValidation()
        {
            var key = new SymmetricSecurityKey(new byte[32]);
            var configuration = new OpenIdConnectConfiguration { Issuer = PublicIssuer(Tenant) };
            configuration.SigningKeys.Add(key);
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                "https://metadata.test/.well-known/openid-configuration",
                new InMemoryConfigurationRetriever(configuration));
            var services = new ServiceCollection();
            services.AddLogging();
            SampleAspNetExtensions.AddAgentAspNetAuthentication(services, CreateSettings(dynamic: true));

            var messageReceived = false;
            var issuerValidated = false;
            var tokenValidated = false;
            string? observedCaller = null;
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters.IssuerSigningKey = key;
                var originalMessageReceived = options.Events.OnMessageReceived;
                options.Events.OnMessageReceived = async context =>
                {
                    messageReceived = true;
                    await originalMessageReceived(context);
                    context.Options.TokenValidationParameters.ConfigurationManager = configurationManager;
                };
                var originalIssuerValidator = options.TokenValidationParameters.IssuerValidator!;
                options.TokenValidationParameters.IssuerValidator = (issuer, token, parameters) =>
                {
                    issuerValidated = true;
                    return originalIssuerValidator(issuer, token, parameters);
                };
                var originalTokenValidated = options.Events.OnTokenValidated;
                options.Events.OnTokenValidated = async context =>
                {
                    tokenValidated = true;
                    observedCaller = context.Principal?.FindFirst("azp")?.Value;
                    await originalTokenValidated(context);
                };
            });

            using var provider = services.BuildServiceProvider();
            var token = CreateSignedToken(new JsonWebTokenHandler(), key, PublicIssuer(Tenant), Audience, DateTime.UtcNow.AddMinutes(30));
            // Authentication handlers are scoped to a request; use a separate scope for each context.
            using var allowedScope = provider.CreateScope();
            var context = new DefaultHttpContext { RequestServices = allowedScope.ServiceProvider };
            context.Request.Headers.Authorization = $"Bearer {token}";
            var result = await context.AuthenticateAsync();

            Assert.True(result.Succeeded, result.Failure?.ToString());
            Assert.True(messageReceived);
            Assert.True(issuerValidated);
            Assert.True(tokenValidated);

            var untrustedToken = CreateSignedToken(new JsonWebTokenHandler(), key, PublicIssuer(Tenant), Audience, DateTime.UtcNow.AddMinutes(30), OtherCaller);
            Assert.Equal(OtherCaller, new JsonWebToken(untrustedToken).GetClaim("azp").Value);
            messageReceived = false;
            issuerValidated = false;
            tokenValidated = false;
            using var deniedScope = provider.CreateScope();
            var deniedContext = new DefaultHttpContext { RequestServices = deniedScope.ServiceProvider };
            deniedContext.Request.Headers.Authorization = $"Bearer {untrustedToken}";
            var deniedResult = await deniedContext.AuthenticateAsync();
            Assert.True(messageReceived);
            Assert.True(issuerValidated);
            Assert.True(tokenValidated);
            Assert.Equal(OtherCaller, observedCaller);
            Assert.False(deniedResult.Succeeded);
            Assert.NotNull(deniedResult.Failure);
        }

        [Fact]
        public async Task DynamicModeRetainsSignatureAudienceAndLifetimeValidation()
        {
            var options = CreateBearerOptions(dynamic: true);
            var key = new SymmetricSecurityKey(new byte[32]);
            var parameters = options.TokenValidationParameters.Clone();
            parameters.IssuerSigningKey = key;
            var handler = new JsonWebTokenHandler();
            var validToken = CreateSignedToken(handler, key, PublicIssuer(Tenant), Audience, DateTime.UtcNow.AddMinutes(30));
            Assert.True((await handler.ValidateTokenAsync(validToken, parameters)).IsValid);

            var wrongAudience = parameters.Clone();
            wrongAudience.ValidAudiences = [OtherTenant];
            Assert.False((await handler.ValidateTokenAsync(validToken, wrongAudience)).IsValid);

            var wrongKey = parameters.Clone();
            wrongKey.IssuerSigningKey = new SymmetricSecurityKey(new byte[32].Select(_ => (byte)1).ToArray());
            Assert.False((await handler.ValidateTokenAsync(validToken, wrongKey)).IsValid);

            var unsignedToken = handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = PublicIssuer(Tenant),
                Audience = Audience,
                Subject = new ClaimsIdentity([new Claim("tid", Tenant)]),
                Expires = DateTime.UtcNow.AddMinutes(30)
            });
            Assert.False((await handler.ValidateTokenAsync(unsignedToken, parameters)).IsValid);

            var expiredToken = handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = PublicIssuer(Tenant),
                Audience = Audience,
                Subject = new ClaimsIdentity([new Claim("tid", Tenant)]),
                NotBefore = DateTime.UtcNow.AddHours(-2),
                Expires = DateTime.UtcNow.AddHours(-1),
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            });
            Assert.False((await handler.ValidateTokenAsync(expiredToken, parameters)).IsValid);
        }

        private static string CreateSignedToken(JsonWebTokenHandler handler, SecurityKey key, string issuer, string audience, DateTime expires, string caller = Caller) =>
            handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = audience,
                Subject = new ClaimsIdentity([new Claim("tid", Tenant), new Claim("azp", caller)]),
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                Expires = expires,
                SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            });

        private static string PublicIssuer(string tenant) => $"https://login.microsoftonline.com/{tenant}/v2.0";

        private static string GovIssuer(string tenant) => $"https://login.microsoftonline.us/{tenant}/v2.0";

        private static TokenValidatedContext CreateValidatedContext(JwtBearerOptions options, string caller) => new(
            new DefaultHttpContext(),
            new AuthenticationScheme(JwtBearerDefaults.AuthenticationScheme, null, typeof(JwtBearerHandler)),
            options)
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("iss", PublicIssuer(Tenant)), new Claim("tid", Tenant), new Claim("azp", caller)],
                JwtBearerDefaults.AuthenticationScheme))
        };

        private static SampleAspNetExtensions.TokenValidationOptions CreateSettings(bool dynamic = false, bool isGov = false) => new()
        {
            Audiences = [Audience],
            AllowDynamicTenantIssuers = dynamic,
            IsGov = isGov,
            AllowedCallers = [Caller]
        };

        private static JwtBearerOptions CreateBearerOptions(bool dynamic = false, bool isGov = false, bool handleBotService = true, bool botServiceOnly = false, IList<string>? validIssuers = null)
        {
            var settings = CreateSettings(dynamic, isGov);
            settings.AzureBotServiceTokenHandling = handleBotService;
            settings.AzureBotServiceOnly = botServiceOnly;
            settings.ValidIssuers = validIssuers;
            var services = new ServiceCollection();
            SampleAspNetExtensions.AddAgentAspNetAuthentication(services, settings);
            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        }

        private static string ValidateIssuer(JwtBearerOptions options, string? issuer, params string[] tenantIds)
        {
            var claims = tenantIds.Select(tenant => new Claim("tid", tenant)).ToList();
            var token = new JsonWebToken(new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = issuer,
                Audience = Audience,
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(new byte[32]), SecurityAlgorithms.HmacSha256)
            }));
            return options.TokenValidationParameters.IssuerValidator!(issuer!, token, options.TokenValidationParameters);
        }

        private sealed class InMemoryConfigurationRetriever(OpenIdConnectConfiguration configuration) : IConfigurationRetriever<OpenIdConnectConfiguration>
        {
            public Task<OpenIdConnectConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever, CancellationToken cancel) =>
                Task.FromResult(configuration);
        }
    }
}
