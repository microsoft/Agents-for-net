// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Connector.RestClients;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector
{
    /// <summary>
    /// The Bot Connector REST API allows your bot to send and receive messages to channels configured in the Azure Bot Service.
    /// The Connector service uses industry-standard REST and JSON over HTTPS.
    /// </summary>
    public class RestConnectorClient : IConnectorClient
    {
        private readonly Uri _endpoint;

        public IAttachments Attachments { get; }

        public IConversations Conversations { get; }

        public Uri BaseUri => _endpoint;

        public RestConnectorClient(Uri endpoint, IHttpClientFactory httpClientFactory, Func<Task<string>> tokenProviderFunction, string namedClient = nameof(RestConnectorClient))
        {
            ArgumentNullException.ThrowIfNull(endpoint);
            ArgumentNullException.ThrowIfNull(httpClientFactory);

            _endpoint = endpoint;

            Conversations = new ConversationsRestClient(endpoint, httpClientFactory, tokenProviderFunction, namedClient);
            Attachments = new AttachmentsRestClient(endpoint, httpClientFactory, tokenProviderFunction, namedClient);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
