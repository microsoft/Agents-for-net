// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Authentication.EntraAuthSidecar.HealthChecks
{
    /// <summary>
    /// Optional hosted service that probes the Microsoft Entra ID Agent ID sidecar once at startup by
    /// calling its <c>/healthz</c> endpoint. Depending on configuration, an unreachable sidecar either
    /// fails fast (throws, preventing startup) or logs a warning and allows startup to continue.
    /// </summary>
    internal sealed class SidecarStartupHealthCheck(
        SidecarHttpClient sidecarClient,
        bool failOnUnreachable,
        TimeSpan timeout,
        ILogger logger = null) : IHostedService
    {
        private readonly SidecarHttpClient _sidecarClient = sidecarClient ?? throw new ArgumentNullException(nameof(sidecarClient));
        private readonly bool _failOnUnreachable = failOnUnreachable;
        private readonly TimeSpan _timeout = timeout;
        private readonly ILogger _logger = logger ?? NullLogger.Instance;

        /// <inheritdoc/>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var timeoutCts = new CancellationTokenSource(_timeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            // IsHealthyAsync never throws; it returns false on any failure to reach the sidecar.
            var healthy = await _sidecarClient.IsHealthyAsync(linkedCts.Token).ConfigureAwait(false);

            if (healthy)
            {
                _logger.LogInformation("Entra ID sidecar startup probe succeeded.");
                return;
            }

            if (_failOnUnreachable)
            {
                throw new InvalidOperationException(
                    "Entra ID sidecar startup probe failed: /healthz did not return a success status.");
            }

            _logger.LogWarning(
                "Entra ID sidecar startup probe failed: /healthz did not return a success status. Startup will continue.");
        }

        /// <inheritdoc/>
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>
    /// Extension methods for registering the optional Entra ID sidecar startup health probe.
    /// </summary>
    public static class SidecarStartupProbeExtensions
    {
        /// <summary>
        /// Registers an opt-in hosted service that probes the Microsoft Entra ID Agent ID sidecar once at
        /// startup via its <c>/healthz</c> endpoint.
        /// </summary>
        /// <remarks>
        /// If a <see cref="SidecarHttpClient"/> has been registered (for example via
        /// <see cref="SidecarServiceCollectionExtensions.AddSidecarConnections"/>) it is reused;
        /// otherwise one is created using <see cref="IHttpClientFactory"/> and the resolved base URL.
        /// </remarks>
        /// <param name="services">The service collection.</param>
        /// <param name="failOnUnreachable">
        /// When <c>true</c>, an unreachable sidecar throws and prevents application startup (fail fast).
        /// When <c>false</c> (the default), a warning is logged and startup continues.
        /// </param>
        /// <param name="sidecarBaseUrl">
        /// Optional sidecar base URL used only when no <see cref="SidecarHttpClient"/> is registered. When
        /// null, resolves from the <c>SIDECAR_URL</c> environment variable, then falls back to the default.
        /// </param>
        /// <param name="bypassLocalNetworkRestriction">
        /// When <c>true</c>, disables the loopback/private-address safety check on the resolved URL.
        /// <b>UNSAFE</b>; see <see cref="SidecarHttpClient.ValidateBaseUrl"/>. Only used when no
        /// <see cref="SidecarHttpClient"/> is already registered.
        /// </param>
        /// <param name="timeout">
        /// Optional probe timeout. Defaults to <see cref="SidecarHttpClient.DefaultTimeout"/>.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSidecarStartupProbe(
            this IServiceCollection services,
            bool failOnUnreachable = false,
            string sidecarBaseUrl = null,
            bool bypassLocalNetworkRestriction = false,
            TimeSpan? timeout = null)
        {
            services.AddSingleton<IHostedService>(sp =>
            {
                var existing = sp.GetService<SidecarHttpClient>();
                var logger = sp.GetService<ILoggerFactory>()?.CreateLogger<SidecarStartupHealthCheck>()
                    ?? (ILogger)NullLogger<SidecarStartupHealthCheck>.Instance;

                SidecarHttpClient client;
                if (existing != null)
                {
                    client = existing;
                }
                else
                {
                    var resolvedUrl = SidecarHttpClient.ResolveBaseUrl(sidecarBaseUrl);
                    SidecarHttpClient.ValidateBaseUrl(resolvedUrl, bypassLocalNetworkRestriction);

                    var httpClientFactory = sp.GetService<IHttpClientFactory>();
                    var httpClient = httpClientFactory?.CreateClient(SidecarHttpClient.HttpClientName)
                        ?? new HttpClient { Timeout = SidecarHttpClient.DefaultTimeout };
                    client = new SidecarHttpClient(httpClient, resolvedUrl, logger);
                }

                return new SidecarStartupHealthCheck(
                    client,
                    failOnUnreachable,
                    timeout ?? SidecarHttpClient.DefaultTimeout,
                    logger);
            });

            return services;
        }

        /// <summary>
        /// Registers an opt-in hosted service that probes the Microsoft Entra ID Agent ID sidecar once at
        /// startup. This builder-phase overload allows the startup probe to be configured fluently alongside
        /// the other agent registration calls:
        /// <code>
        /// builder.AddAgentDefaults()
        ///     .AddAgent&lt;MyAgent&gt;()
        ///     .AddSidecarStartupProbe(failOnUnreachable: false)
        ///     .AddAgentAuthorization(b =&gt; b.AddAgentAspNetAuthentication());
        /// </code>
        /// </summary>
        /// <remarks>
        /// This is equivalent to <c>builder.Services.AddSidecarStartupProbe(...)</c>. See
        /// <see cref="AddSidecarStartupProbe(IServiceCollection, bool, string, bool, TimeSpan?)"/> for parameter details.
        /// </remarks>
        /// <param name="builder">The host application builder.</param>
        /// <param name="failOnUnreachable">When <c>true</c>, an unreachable sidecar throws and prevents startup. Defaults to <c>false</c>.</param>
        /// <param name="sidecarBaseUrl">Optional sidecar base URL used only when no <see cref="SidecarHttpClient"/> is registered.</param>
        /// <param name="bypassLocalNetworkRestriction">When <c>true</c>, disables the loopback/private-address safety check. <b>UNSAFE</b>.</param>
        /// <param name="timeout">Optional probe timeout. Defaults to <see cref="SidecarHttpClient.DefaultTimeout"/>.</param>
        /// <returns>The same host application builder for chaining.</returns>
        public static IHostApplicationBuilder AddSidecarStartupProbe(
            this IHostApplicationBuilder builder,
            bool failOnUnreachable = false,
            string sidecarBaseUrl = null,
            bool bypassLocalNetworkRestriction = false,
            TimeSpan? timeout = null)
        {
            builder.Services.AddSidecarStartupProbe(failOnUnreachable, sidecarBaseUrl, bypassLocalNetworkRestriction, timeout);
            return builder;
        }
    }
}
