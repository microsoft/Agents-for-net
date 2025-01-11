// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class BotSignInRestClient(Uri endpoint,
        IHttpClientFactory httpClientFactory,
        Func<Task<string>> tokenProviderFunction,
        string httpClientName = nameof(RestUserTokenClient)) : RestClientBase(httpClientFactory, httpClientName, tokenProviderFunction), IBotSignIn
    {
        private readonly Uri _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

        internal HttpRequestMessage CreateGetSignInUrlRequest(string state, string codeChallenge, string emulatorUrl, string finalRedirect)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,

                RequestUri = new Uri(endpoint, $"api/botsignin/GetSignInUrl")
                    .AppendQuery("state", state)
                    .AppendQuery("code_challenge", codeChallenge)
                    .AppendQuery("emulatorUrl", emulatorUrl)
                    .AppendQuery("finalRedirect", finalRedirect)
            };

            request.Headers.Add("Accept", "text/plain");
            return request;
        }

        /// <inheritdoc/>
        public async Task<string> GetSignInUrlAsync(string state, string codeChallenge = null, string emulatorUrl = null, string finalRedirect = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(state);

            using var message = CreateGetSignInUrlRequest(state, codeChallenge, emulatorUrl, finalRedirect);
            using var httpClient = await GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    }
                default:
                    throw new HttpRequestException($"GetSignInUrlAsync {httpResponse.StatusCode}");
            }
        }

        internal HttpRequestMessage CreateGetSignInResourceRequest(string state, string codeChallenge, string emulatorUrl, string finalRedirect)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,

                RequestUri = new Uri(endpoint, $"api/botsignin/GetSignInResource")
                    .AppendQuery("state", state)
                    .AppendQuery("code_challenge", codeChallenge)
                    .AppendQuery("emulatorUrl", emulatorUrl)
                    .AppendQuery("finalRedirect", finalRedirect)
            };

            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <inheritdoc/>
        public async Task<SignInResource> GetSignInResourceAsync(string state, string codeChallenge = null, string emulatorUrl = null, string finalRedirect = null, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(state);

            using var message = CreateGetSignInResourceRequest(state, codeChallenge, emulatorUrl, finalRedirect);
            using var httpClient = await GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<SignInResource>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    throw new HttpRequestException($"GetSignInResourceAsync {httpResponse.StatusCode}");
            }
        }
    }
}
