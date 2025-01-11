// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector.Teams
{
    /// <summary>
    /// TeamsConnectorClient REST implementation.  This ConnectorClient is suitable for either ABS or SMBA.
    /// </summary>
    public class RestTeamsConnectorClient(Uri endpoint, IHttpClientFactory httpClientFactory, Func<Task<string>> tokenProviderFunction, string namedClient = nameof(RestTeamsConnectorClient)) 
        : RestConnectorClient(endpoint, httpClientFactory, tokenProviderFunction, namedClient), ITeamsConnectorClient
    {

        /// <inheritdoc/>
        public ITeamsOperations Teams { get; private set; } = new RestTeamsOperations(httpClientFactory, namedClient, tokenProviderFunction);
    }
}
