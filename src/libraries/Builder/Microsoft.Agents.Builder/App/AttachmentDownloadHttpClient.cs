// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
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

        private readonly IOutboundHostValidator _hostValidator;
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
            _hostValidator = hostValidator ?? throw new ArgumentNullException(nameof(hostValidator));
            _hostAddressResolver = hostAddressResolver ?? throw new ArgumentNullException(nameof(hostAddressResolver));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<HttpResponseMessage?> SendAsync(
            Uri requestUri,
            Action<HttpRequestMessage>? configureInitialRequest,
            CancellationToken cancellationToken)
        {
            var currentUri = requestUri;

            for (var redirectCount = 0; ; redirectCount++)
            {
                if (!await IsAllowedAsync(currentUri, cancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                if (redirectCount == 0)
                {
                    configureInitialRequest?.Invoke(request);
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
                response.Dispose();
            }
        }

        private async Task<bool> IsAllowedAsync(Uri uri, CancellationToken cancellationToken)
        {
            if (_hostValidator is OutboundHostValidator defaultValidator)
            {
                return await defaultValidator.IsAllowedAsync(uri, cancellationToken, _hostAddressResolver).ConfigureAwait(false);
            }

            if (uri == null
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !_hostValidator.IsAllowed(uri))
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
