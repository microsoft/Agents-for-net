// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.UserAuth.EntraSidecar
{
    /// <summary>
    /// ASP.NET Core health check that reports whether the Microsoft Entra ID Agent ID sidecar is
    /// reachable, by calling its <c>/healthz</c> endpoint.
    /// </summary>
    internal sealed class SidecarHealthCheck : IHealthCheck
    {
        private readonly SidecarHttpClient _sidecarClient;

        public SidecarHealthCheck(SidecarHttpClient sidecarClient) => _sidecarClient = sidecarClient;

        /// <inheritdoc/>
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // IsHealthyAsync never throws; it returns false on any failure to reach the sidecar.
            var healthy = await _sidecarClient.IsHealthyAsync(cancellationToken).ConfigureAwait(false);

            return healthy
                ? HealthCheckResult.Healthy("Entra ID sidecar is reachable.")
                : new HealthCheckResult(
                    context.Registration.FailureStatus,
                    "Entra ID sidecar /healthz did not return a success status.");
        }
    }
}
