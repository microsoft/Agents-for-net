// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Protocols.Connector;
using Microsoft.Agents.Teams.Primitives;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Microsoft.Agents.Teams.Connector
{
    /// <summary>
    /// TeamsConnectorClient REST implementation.  This ConnectorClient is suitable for either ABS or SMBA.
    /// </summary>
    internal class RestTeamsConnectorClient : RestConnectorClient, ITeamsConnectorClient
    {
        public RestTeamsConnectorClient(Uri endpoint, HttpClient httpClient, string resource, IList<string> scopes = null, bool useAnonymousConnection = false)
            : this(endpoint, httpClient, resource, scopes, new ConnectorClientOptions(), useAnonymousConnection)
        {
        }

        public RestTeamsConnectorClient(Uri endpoint, HttpClient httpClient, string resource, IList<string> scopes, ConnectorClientOptions options, bool useAnonymousConnection = false)
            : base(endpoint, httpClient, resource, scopes, options, useAnonymousConnection)
        {
            Teams = new RestTeamsOperations(this, httpClient);
        }

        /// <inheritdoc/>
        public ITeamsOperations Teams { get; private set; }
    }
}
