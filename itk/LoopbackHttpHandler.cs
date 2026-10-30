// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace AgentsA2AItk;

internal sealed class LoopbackHttpHandler : DelegatingHandler
{
    internal static bool IsAllowed(Uri? uri) => uri is not null
        && uri.Scheme == Uri.UriSchemeHttp
        && (uri.Host == "127.0.0.1" || uri.Host == "[::1]" || uri.Host == "::1");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!IsAllowed(request.RequestUri))
        {
            throw new InvalidOperationException("ITK peer requests must use loopback HTTP endpoints.");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
