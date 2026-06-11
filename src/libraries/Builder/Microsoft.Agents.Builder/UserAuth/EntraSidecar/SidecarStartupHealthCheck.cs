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

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
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
        /// <param name="timeout">
        /// Optional probe timeout. Defaults to <see cref="SidecarHttpClient.DefaultTimeout"/>.
        /// </param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddSidecarStartupProbe(
            this IServiceCollection services,
            bool failOnUnreachable = false,
            string sidecarBaseUrl = null,
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
                    var httpClientFactory = sp.GetService<IHttpClientFactory>();
                    var httpClient = httpClientFactory?.CreateClient(SidecarHttpClient.HttpClientName)
                        ?? new HttpClient { Timeout = SidecarHttpClient.DefaultTimeout };
                    client = new SidecarHttpClient(httpClient, SidecarHttpClient.ResolveBaseUrl(sidecarBaseUrl), logger);
                }

                return new SidecarStartupHealthCheck(
                    client,
                    failOnUnreachable,
                    timeout ?? SidecarHttpClient.DefaultTimeout,
                    logger);
            });

            return services;
        }
    }
}
