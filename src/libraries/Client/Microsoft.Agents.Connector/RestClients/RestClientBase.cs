// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using Microsoft.Agents.Core;

namespace Microsoft.Agents.Connector.RestClients
{
    public class RestClientBase(Uri endpoint, IHttpClientFactory httpClientFactory, string httpClientName, Func<Task<string>> tokenProviderFunction) : IRestTransport
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly Func<Task<string>> _tokenProviderFunction = tokenProviderFunction;
        private readonly string _httpClientName = httpClientName ?? throw new ArgumentNullException(nameof(httpClientName));
        private readonly Uri _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

        public Uri Endpoint => _endpoint;

        public async Task<HttpClient> GetHttpClientAsync()
        {
            var httpClient = _httpClientFactory.CreateClient(_httpClientName);

            ProductInfoHeaderValue[] additionalProductInfo = null;
            IHeaderPropagation propagateHeaders = RequestContext.GetHeaderPropagation();
            if (propagateHeaders != null)
            {
                if (propagateHeaders.Headers != null)
                {
                    foreach (var header in propagateHeaders.Headers)
                    {
                        httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                if (!string.IsNullOrEmpty(propagateHeaders?.UserAgent))
                {
                    additionalProductInfo = [new ProductInfoHeaderValue(ProductHeaderValue.Parse(propagateHeaders.UserAgent))];
                }
            }

            httpClient.AddDefaultUserAgent(additionalProductInfo);

            if (_tokenProviderFunction != null)
            {
                var token = await _tokenProviderFunction().ConfigureAwait(false);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return httpClient;
        }
    }
}
