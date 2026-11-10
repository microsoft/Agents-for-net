// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Builder.App
{
    internal sealed class AttachmentDownloadHttpClient
    {
        private const int MaxAutomaticRedirections = 50;
        private static readonly HttpClient DefaultHttpClient = new(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });

        private readonly IOutboundHostValidator? _hostValidator;
        private readonly IHostAddressResolver _hostAddressResolver;
        private readonly HttpClient _httpClient;

        public AttachmentDownloadHttpClient(IOutboundHostValidator hostValidator, IHostAddressResolver hostAddressResolver)
            : this(hostValidator, hostAddressResolver, DefaultHttpClient)
        {
        }

        internal AttachmentDownloadHttpClient(
            IOutboundHostValidator hostValidator,
            IHostAddressResolver hostAddressResolver,
            HttpClient httpClient)
        {
            _hostValidator = hostValidator;
            _hostAddressResolver = hostAddressResolver ?? throw new ArgumentNullException(nameof(hostAddressResolver));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public Task<HttpResponseMessage?> SendAsync(
            Uri requestUri,
            Action<HttpRequestMessage>? configureInitialRequest,
            CancellationToken cancellationToken)
            => SendAsync(requestUri, configureInitialRequest, requireHttps: false, retainAuthorizationOnSameOriginRedirects: false, cancellationToken);

        public async Task<HttpResponseMessage?> SendAsync(
            Uri requestUri,
            Action<HttpRequestMessage>? configureInitialRequest,
            bool requireHttps,
            bool retainAuthorizationOnSameOriginRedirects,
            CancellationToken cancellationToken)
        {
            var currentUri = requestUri;
            AuthenticationHeaderValue? authorization = null;
            bool sameOrigin = true;

            for (var redirectCount = 0; ; redirectCount++)
            {
                if ((requireHttps && currentUri?.Scheme != Uri.UriSchemeHttps)
                    || !await IsAllowedAsync(currentUri, cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                if (redirectCount == 0)
                {
                    configureInitialRequest?.Invoke(request);
                    authorization = request.Headers.Authorization;
                }
                else if (retainAuthorizationOnSameOriginRedirects && sameOrigin)
                {
                    request.Headers.Authorization = authorization;
                }

                var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (!IsRedirect(response.StatusCode) || response.Headers.Location == null)
                {
                    return response;
                }

                if (redirectCount >= MaxAutomaticRedirections)
                {
                    response.Dispose();
                    throw new HttpRequestException($"The maximum number of redirects ({MaxAutomaticRedirections}) was exceeded.");
                }

                currentUri = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location
                    : new Uri(currentUri, response.Headers.Location);
                sameOrigin = sameOrigin && Uri.Compare(
                    currentUri, requestUri, UriComponents.SchemeAndServer, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
                response.Dispose();
            }
        }

        private async Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (uri == null || !uri.IsAbsoluteUri
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            if (_hostValidator?.Enabled != true)
            {
                return true;
            }

            if (_hostValidator is OutboundHostValidator defaultValidator)
            {
                return await defaultValidator.IsAllowedAsync(uri, cancellationToken, _hostAddressResolver).ConfigureAwait(false);
            }

            if (!_hostValidator.IsAllowed(uri))
            {
                return false;
            }

            if (IPAddress.TryParse(uri.IdnHost, out var address))
            {
                return !OutboundHostAddressValidation.IsBlockedAddress(address);
            }

            return await OutboundHostAddressValidation.AreAddressesAllowedAsync(
                uri.IdnHost,
                _hostAddressResolver,
                cancellationToken).ConfigureAwait(false);
        }

        private static bool IsRedirect(HttpStatusCode statusCode)
        {
            return statusCode == HttpStatusCode.MultipleChoices
                || statusCode == HttpStatusCode.MovedPermanently
                || statusCode == HttpStatusCode.Redirect
                || statusCode == HttpStatusCode.SeeOther
                || statusCode == HttpStatusCode.TemporaryRedirect
                || (int)statusCode == 308;
        }

    }
}
