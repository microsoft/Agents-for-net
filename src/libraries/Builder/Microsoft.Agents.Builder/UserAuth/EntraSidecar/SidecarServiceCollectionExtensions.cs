// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// Extension methods for registering Entra SDK Sidecar services in DI.
    /// </summary>
    public static class SidecarServiceCollectionExtensions
    {
        /// <summary>
        /// Registers the Entra SDK Sidecar as the connection-level token provider,
        /// replacing MSAL for token acquisition. This registers <see cref="SidecarAccessTokenProvider"/>
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
            var settings = section.Get<SidecarSettings>() ?? new SidecarSettings();
            var baseUrl = SidecarHttpClient.ResolveBaseUrl(settings.SidecarBaseUrl);

            services.AddHttpClient(SidecarHttpClient.HttpClientName, client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = SidecarHttpClient.DefaultTimeout;
            });

            services.AddSingleton(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(SidecarHttpClient.HttpClientName);
                return new SidecarHttpClient(httpClient, baseUrl);
            });

            services.AddSingleton(sp =>
            {
                var sidecarClient = sp.GetRequiredService<SidecarHttpClient>();

                // Bind the full connection settings from the same section so BlueprintServiceName, Scopes,
                // and inherited connection settings (e.g. TenantId) are preserved rather than dropped.
                var connectionSettings = section.Get<SidecarConnectionSettings>() ?? new SidecarConnectionSettings();
                if (string.IsNullOrEmpty(connectionSettings.ServiceName))
                {
                    connectionSettings.ServiceName = "default";
                }

                return new SidecarAccessTokenProvider(sidecarClient, connectionSettings);
            });

            services.AddSingleton<IAccessTokenProvider>(sp => sp.GetRequiredService<SidecarAccessTokenProvider>());
            services.AddSingleton<IAgenticTokenProvider>(sp => sp.GetRequiredService<SidecarAccessTokenProvider>());

            return services;
        }
    }
}
