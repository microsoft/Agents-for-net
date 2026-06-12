// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Collections.Generic;
using System.Net.Http;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar.HealthChecks
{
    /// <summary>
    /// Extension methods for registering the Entra ID sidecar ASP.NET Core health check.
    /// </summary>
    public static class SidecarHealthCheckBuilderExtensions
    {
        /// <summary>
        /// Adds a health check that verifies the Microsoft Entra ID Agent ID sidecar is reachable
        /// via its <c>/healthz</c> endpoint.
        /// </summary>
        /// <remarks>
        /// If a <see cref="SidecarHttpClient"/> has been registered (for example via
        /// <see cref="SidecarServiceCollectionExtensions.AddSidecarConnections"/>) it is reused;
        /// otherwise one is created using <see cref="IHttpClientFactory"/> and the resolved base URL.
        /// </remarks>
        /// <param name="builder">The health checks builder.</param>
        /// <param name="name">The health check name. Defaults to <c>entra_sidecar</c>.</param>
        /// <param name="sidecarBaseUrl">
        /// Optional sidecar base URL. When null, resolves from the <c>SIDECAR_URL</c> environment variable,
        /// then falls back to <c>http://localhost:5178</c>.
        /// </param>
        /// <param name="bypassLocalNetworkRestriction">
        /// When <c>true</c>, disables the loopback/private-address safety check on the resolved URL.
        /// <b>UNSAFE</b>; see <see cref="SidecarHttpClient.ValidateBaseUrl"/>. Only used when no
        /// <see cref="SidecarHttpClient"/> is already registered.
        /// </param>
        /// <param name="failureStatus">
        /// The <see cref="HealthStatus"/> reported when the sidecar is unreachable. Defaults to
        /// <see cref="HealthStatus.Unhealthy"/>.
        /// </param>
        /// <param name="tags">Optional tags used to filter health checks.</param>
        /// <returns>The health checks builder for chaining.</returns>
        public static IHealthChecksBuilder AddSidecarHealthCheck(
            this IHealthChecksBuilder builder,
            string name = "entra_sidecar",
            string sidecarBaseUrl = null,
            bool bypassLocalNetworkRestriction = false,
            HealthStatus? failureStatus = null,
            IEnumerable<string> tags = null)
        {
            // Build the fallback client at most once and reuse it across probes; the registration
            // factory is invoked on every health evaluation, so creating a client (and HttpClient)
            // each time would leak sockets under frequent probing.
            SidecarHttpClient fallbackClient = null;
            var fallbackGate = new object();

            return builder.Add(new HealthCheckRegistration(
                name,
                sp =>
                {
                    var existing = sp.GetService<SidecarHttpClient>();
                    if (existing != null)
                    {
                        return new SidecarHealthCheck(existing);
                    }

                    if (fallbackClient == null)
                    {
                        lock (fallbackGate)
                        {
                            if (fallbackClient == null)
                            {
                                var resolvedUrl = SidecarHttpClient.ResolveBaseUrl(sidecarBaseUrl);
                                SidecarHttpClient.ValidateBaseUrl(resolvedUrl, bypassLocalNetworkRestriction);

                                var httpClientFactory = sp.GetService<IHttpClientFactory>();
                                var httpClient = httpClientFactory?.CreateClient(SidecarHttpClient.HttpClientName)
                                    ?? new HttpClient { Timeout = SidecarHttpClient.DefaultTimeout };

                                fallbackClient = new SidecarHttpClient(httpClient, resolvedUrl);
                            }
                        }
                    }

                    return new SidecarHealthCheck(fallbackClient);
                },
                failureStatus,
                tags));
        }
    }
}
