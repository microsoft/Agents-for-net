// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Builder.Testing.Http
{
    internal sealed class HttpSimulationHandler(HttpSimulation simulation, string clientName, HttpMessageHandler displaced) : HttpMessageHandler
    {
        private HttpMessageHandler _displaced = displaced;
        private int _disposed;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AssertionHelpers.ThrowIfObjectDisposed(Volatile.Read(ref _disposed) != 0, nameof(HttpSimulationHandler));

            return simulation.SendAsync(request, clientName, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Interlocked.Exchange(ref _displaced, null)?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
