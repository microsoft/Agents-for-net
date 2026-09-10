// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core.Models;
using System;
using System.Net.Http;
using System.Security.Claims;

namespace Microsoft.Agents.Extensions.MSTeams.Tests;

/// <summary>
/// Test turn context that mirrors the REST clients registered by ChannelServiceAdapterBase.
/// Tests can replace either service to exercise custom or invalid client implementations.
/// </summary>
internal class TurnContext : Microsoft.Agents.Builder.TurnContext
{
    private static readonly IHttpClientFactory HttpClientFactory = new TestHttpClientFactory();

    public TurnContext(IChannelAdapter adapter, IActivity activity, ClaimsIdentity identity = null)
        : base(adapter, activity, identity)
    {
        var serviceUrl = Uri.TryCreate(activity.ServiceUrl, UriKind.Absolute, out var endpoint)
            ? endpoint
            : new Uri("https://smba.trafficmanager.net/amer/");

        Services.Set<IConnectorClient>(new RestConnectorClient(serviceUrl, HttpClientFactory, tokenProviderFunction: null));
        Services.Set<IUserTokenClient>(new RestUserTokenClient(
            "test-agent",
            new Uri("https://token.botframework.com/"),
            HttpClientFactory,
            tokenProviderFunction: null));
    }

    public TurnContext(ITurnContext turnContext, IActivity activity)
        : base(turnContext, activity)
    {
    }
}
