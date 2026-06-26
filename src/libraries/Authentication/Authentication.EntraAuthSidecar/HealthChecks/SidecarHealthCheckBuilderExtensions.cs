// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
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
        /// Adds a health check that verifies the Microsoft Entra ID Agent ID sidecar is reachable, registering
        /// the ASP.NET Core health checks service if necessary. This builder-phase overload allows the sidecar
        /// health check to be configured fluently alongside the other agent registration calls:
        /// <code>
        /// builder.AddAgentDefaults()
        ///     .AddAgent&lt;MyAgent&gt;()
        ///     .AddSidecarHealthCheck()
        ///     .AddAgentAuthorization(b =&gt; b.AddAgentAspNetAuthentication());
        /// </code>
        /// </summary>
        /// <remarks>
        /// This is equivalent to <c>builder.Services.AddHealthChecks().AddSidecarHealthCheck(...)</c>. To expose
        /// the result, map the health endpoint on the built application (e.g. <c>app.MapHealthChecks("/health")</c>).
        /// See <see cref="AddSidecarHealthCheck(IHealthChecksBuilder, string, string, bool, HealthStatus?, IEnumerable{string})"/>
        /// for parameter details.
        /// </remarks>
        /// <param name="builder">The host application builder.</param>
        /// <param name="name">The health check name. Defaults to <c>entra_sidecar</c>.</param>
        /// <param name="sidecarBaseUrl">Optional sidecar base URL. When null, resolves from <c>SIDECAR_URL</c>, then the default.</param>
        /// <param name="bypassLocalNetworkRestriction">When <c>true</c>, disables the loopback/private-address safety check. <b>UNSAFE</b>.</param>
        /// <param name="failureStatus">The status reported when the sidecar is unreachable. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
        /// <param name="tags">Optional tags used to filter health checks.</param>
        /// <returns>The same host application builder for chaining.</returns>
        public static IHostApplicationBuilder AddSidecarHealthCheck(
            this IHostApplicationBuilder builder,
            string name = "entra_sidecar",
            string sidecarBaseUrl = null,
            bool bypassLocalNetworkRestriction = false,
            HealthStatus? failureStatus = null,
            IEnumerable<string> tags = null)
        {
            builder.Services.AddHealthChecks().AddSidecarHealthCheck(
                name, sidecarBaseUrl, bypassLocalNetworkRestriction, failureStatus, tags);
            return builder;
        }

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

                    // Single lock around both initialization and read guarantees safe publication of
                    // the shared fallback client across concurrent probes. The lock is uncontended in
                    // practice (probes are infrequent), so the cost is negligible.
                    SidecarHttpClient client;
                    lock (fallbackGate)
                    {
                        client = fallbackClient ??= CreateFallbackClient(sp, sidecarBaseUrl, bypassLocalNetworkRestriction);
                    }

                    return new SidecarHealthCheck(client);
                },
                failureStatus,
                tags));
        }

        private static SidecarHttpClient CreateFallbackClient(
            System.IServiceProvider sp,
            string sidecarBaseUrl,
            bool bypassLocalNetworkRestriction)
        {
            var resolvedUrl = SidecarHttpClient.ResolveBaseUrl(sidecarBaseUrl);
            SidecarHttpClient.ValidateBaseUrl(resolvedUrl, bypassLocalNetworkRestriction);

            var httpClientFactory = sp.GetService<IHttpClientFactory>();
            var httpClient = httpClientFactory?.CreateClient(SidecarHttpClient.HttpClientName)
                ?? new HttpClient { Timeout = SidecarHttpClient.DefaultTimeout };

            return new SidecarHttpClient(httpClient, resolvedUrl);
        }
    }
}
