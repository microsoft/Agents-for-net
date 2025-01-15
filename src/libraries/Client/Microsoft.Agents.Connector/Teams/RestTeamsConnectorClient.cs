// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector.Teams
{
    /// <summary>
    /// TeamsConnectorClient REST implementation.  This ConnectorClient is suitable for either ABS or SMBA.
    /// </summary>
    public class RestTeamsConnectorClient : RestConnectorClient, ITeamsConnectorClient
    {
        /// <inheritdoc/>
        public ITeamsOperations Teams { get; private set; }

        public RestTeamsConnectorClient(Uri endpoint, IHttpClientFactory httpClientFactory, Func<Task<string>> tokenProviderFunction, string namedClient = nameof(RestTeamsConnectorClient)) : base(endpoint, httpClientFactory, tokenProviderFunction, namedClient)
        {
            Teams = new RestTeamsOperations(httpClientFactory, namedClient, tokenProviderFunction) { Client = this };
        }
    }
}
