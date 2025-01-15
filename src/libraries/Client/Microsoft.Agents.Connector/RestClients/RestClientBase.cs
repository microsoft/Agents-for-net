// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using System;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class RestClientBase(IHttpClientFactory httpClientFactory, string httpClientName, Func<Task<string>> tokenProviderFunction)
    {
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        private readonly Func<Task<string>> _tokenProviderFunction = tokenProviderFunction;
        private readonly string _httpClientName = httpClientName ?? throw new ArgumentNullException(nameof(httpClientName));

        protected async Task<HttpClient> GetHttpClientAsync()
        {
            var httpClient = _httpClientFactory.CreateClient(_httpClientName);

            if (_tokenProviderFunction != null)
            {
                var token = await _tokenProviderFunction().ConfigureAwait(false);
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            httpClient.AddDefaultUserAgent();

            return httpClient;
        }
    }
}
