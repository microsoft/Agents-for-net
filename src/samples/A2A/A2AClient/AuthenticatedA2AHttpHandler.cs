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
    private readonly Uri _agentOrigin;

    public AuthenticatedA2AHttpHandler(
        A2AAuthenticationSession session,
        IA2AAccessTokenProvider accessTokenProvider,
        Uri agentUrl)
        : this(session, accessTokenProvider, agentUrl, CreateInnerHandler())
    {
    }

    internal AuthenticatedA2AHttpHandler(
        A2AAuthenticationSession session,
        IA2AAccessTokenProvider accessTokenProvider,
        Uri agentUrl,
        HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _accessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
        _agentOrigin = A2AAgentOrigin.FromAgentUrl(agentUrl ?? throw new ArgumentNullException(nameof(agentUrl)));
    }

    /// <summary>
    /// Redirects are followed by the inner handler, below this one, so a redirect target would never be
    /// checked against the configured agent origin. Automatic redirects are therefore disabled instead of
    /// re-implementing redirect handling here.
    /// </summary>
    internal static HttpMessageHandler CreateInnerHandler()
        => new HttpClientHandler { AllowAutoRedirect = false };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Headers.Authorization = null;

        string? token = await _accessTokenProvider.GetAccessTokenAsync(_session.Mode, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(token))
        {
            // Throws before the request is sent, so a foreign or plaintext destination never sees the token.
            A2AAgentOrigin.EnsureCredentialTarget(_agentOrigin, request.RequestUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
