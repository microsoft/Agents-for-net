// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Authentication.EntraAuthSidecar.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar
{
    /// <summary>
    /// Extension methods for registering Entra SDK Sidecar services in DI.
    /// </summary>
    public static class SidecarServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Entra SDK Sidecar as the connection-level token provider,
        /// replacing MSAL for token acquisition. This registers <see cref="SidecarAuth"/>
        /// as both <see cref="IAccessTokenProvider"/> and <see cref="IAgenticTokenProvider"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">Application configuration containing the sidecar settings section.</param>
        /// <param name="configSectionName">Name of the configuration section. Defaults to "EntraSidecar".</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSidecarConnections(
            this IServiceCollection services,
            IConfiguration configuration,
            string configSectionName = "EntraSidecar")
        {
            var section = configuration.GetSection(configSectionName);
            var settings = section.Get<SidecarConnectionSettings>() ?? new SidecarConnectionSettings();
            if (string.IsNullOrEmpty(settings.ServiceName))
            {
                settings.ServiceName = "default";
            }

            var baseUrl = SidecarHttpClient.ResolveBaseUrl(settings.SidecarBaseUrl);

            // SSRF safety: the resolved URL (config or SIDECAR_URL env) must point to a loopback/private
            // address before we send the agent's credentials to it, unless the operator has explicitly
            // opted out via BypassLocalNetworkRestriction.
            SidecarHttpClient.ValidateBaseUrl(baseUrl, settings.BypassLocalNetworkRestriction);

            var requestTimeout = settings.RequestTimeout;
            var retryCount = settings.RetryCount;

            services.AddHttpClient(SidecarHttpClient.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = requestTimeout;
            });

            services.AddSingleton(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(SidecarHttpClient.HttpClientName);
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<SidecarHttpClient>();
                return new SidecarHttpClient(httpClient, baseUrl, logger, requestTimeout, retryCount);
            });

            services.AddSingleton(sp =>
            {
                var sidecarClient = sp.GetRequiredService<SidecarHttpClient>();
                return new SidecarAuth(sidecarClient, settings);
            });

            services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<SidecarAuth>());
            services.AddSingleton<IAgenticTokenProvider>(sp => sp.GetRequiredService<SidecarAuth>());

            return services;
        }
    }
}
