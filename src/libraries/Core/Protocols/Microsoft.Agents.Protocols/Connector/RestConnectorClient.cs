// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Protocols.Primitives;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Microsoft.Agents.Protocols.Connector
{
    /// <summary>
    /// The Bot Connector REST API allows your bot to send and receive messages to channels configured in the Azure Bot Service.
    /// The Connector service uses industry-standard REST and JSON over HTTPS.
    /// </summary>
    internal class RestConnectorClient : IConnectorClient
    {
        private readonly Uri _endpoint;

        public IAttachments Attachments { get; }

        public IConversations Conversations { get; }

        public Uri BaseUri => _endpoint;

        public RestConnectorClient(Uri endpoint, HttpClient httpClient, string resource, IList<string> scopes = null, bool useAnonymousConnection = false)
            : this(endpoint, httpClient, resource, scopes, new ConnectorClientOptions(), useAnonymousConnection)
        {
        }

        public RestConnectorClient(Uri endpoint, HttpClient httpClient, string resource, IList<string> scopes, ConnectorClientOptions options, bool useAnonymousConnection = false)
        {
            _endpoint = endpoint;

            Conversations = new ConversationsRestClient(httpClient, endpoint);     
            Attachments = new AttachmentsRestClient(httpClient, endpoint);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
