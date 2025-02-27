// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector
{
    public interface IRestTransport
    {
        Uri Endpoint { get; }
        Task<HttpClient> GetHttpClientAsync();
    }
}
