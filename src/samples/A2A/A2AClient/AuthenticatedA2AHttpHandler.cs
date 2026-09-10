// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class AuthenticatedA2AHttpHandler
    : DelegatingHandler
{
    private readonly A2AAuthenticationSession _session;
    private readonly IA2AAccessTokenProvider _accessTokenProvider;

    public AuthenticatedA2AHttpHandler(A2AAuthenticationSession session, IA2AAccessTokenProvider accessTokenProvider)
        : base(new HttpClientHandler())
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = null;

        string? token = await _accessTokenProvider.GetAccessTokenAsync(_session.Mode, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
