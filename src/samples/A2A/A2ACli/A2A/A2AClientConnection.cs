// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Grpc.Net.Client;
using System;
using System.Net.Http;

namespace Microsoft.Agents.Samples.A2ACli.A2A;

internal sealed class A2AClientConnection : IDisposable
{
    internal A2AClientConnection(string protocolBinding, Uri address, HttpClient httpClient)
    {
        ProtocolBinding = protocolBinding;
        Address = address;
        if (string.Equals(protocolBinding, ProtocolBindingNames.Grpc, StringComparison.OrdinalIgnoreCase))
        {
            Channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                HttpClient = httpClient,
                DisposeHttpClient = false,
            });
            try
            {
                Client = new global::A2A.Grpc.A2AGrpcClient(Channel);
            }
            catch
            {
                Channel.Dispose();
                throw;
            }
        }
        else
        {
            Client = string.Equals(protocolBinding, ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase)
                ? new global::A2A.A2AClient(address, httpClient)
                : new A2AHttpJsonClient(address, httpClient);
        }
    }

    internal IA2AClient Client { get; }

    internal string ProtocolBinding { get; }

    internal Uri Address { get; }

    internal GrpcChannel? Channel { get; }

    public void Dispose() => Channel?.Dispose();
}
