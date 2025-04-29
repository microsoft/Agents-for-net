// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Connector.Errors;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class AgentSignInRestClient(IRestTransport transport) : IAgentSignIn
    {
        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        internal HttpRequestMessage CreateGetSignInUrlRequest(string state, string codeChallenge, string emulatorUrl, string finalRedirect)
        {
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Get,

                RequestUri = new Uri(_transport.Endpoint, $"api/botsignin/GetSignInUrl")
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
            AssertionHelpers.ThrowIfNullOrEmpty(state, nameof(state));

            using var message = CreateGetSignInUrlRequest(state, codeChallenge, emulatorUrl, finalRedirect);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                        return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
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

                RequestUri = new Uri(_transport.Endpoint, $"api/botsignin/GetSignInResource")
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
            AssertionHelpers.ThrowIfNullOrEmpty(state, nameof(state));

            using var message = CreateGetSignInResourceRequest(state, codeChallenge, emulatorUrl, finalRedirect);
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
#if !NETSTANDARD
                        return ProtocolJsonSerializer.ToObject<SignInResource>(httpResponse.Content.ReadAsStream(cancellationToken));
#else
                        return ProtocolJsonSerializer.ToObject<SignInResource>(httpResponse.Content.ReadAsStringAsync().Result);
#endif
                    }
                case 400:
                    {
                        throw ErrorResponseException.CreateErrorResponseException(httpResponse, ErrorHelper.GetSignInResourceAsync_BadRequestError, cancellationToken: cancellationToken);
                    }
                default:

                    throw new HttpRequestException($"GetSignInResourceAsync {httpResponse.StatusCode}");
            }
        }
    }
}
