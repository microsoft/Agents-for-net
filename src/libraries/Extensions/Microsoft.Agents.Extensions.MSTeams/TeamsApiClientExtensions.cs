// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Provides extension methods for configuring the Teams <see cref="ApiClient"/> within an agents's turn context.
/// </summary>
/// <remarks>
/// These extension methods enable integration with the Teams API by associating an ApiClient
/// instance with the <see cref="ITurnContext"/>. This allows agent developers to access Teams-specific functionality during a
/// conversation turn. The methods support both direct configuration and configuration via an AgentApplication
/// instance.<br/><br/>
/// This creates HttpClients named "TeamsHttpClientFactory".
/// </remarks>
internal static class TeamsApiClientExtensions
{
    /// <summary>
    /// Configures the Teams API client for the specified turn context using the provided agent application
    /// settings.
    /// </summary>
    /// <remarks>This extension method initializes the Teams API client for the given context based on
    /// the application's connection and HTTP client factory settings. Ensure that the application parameter is not
    /// null to avoid configuration errors.</remarks>
    /// <param name="context">The turn context in which to set up the Teams API client.</param>
    /// <param name="application">The agent application containing configuration options for the Teams API client. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the operation.</param>
    internal static Task SetTeamsApiClient(this ITurnContext context, AgentApplication application, CancellationToken ct = default)
    {
        AssertionHelpers.ThrowIfNull(application, nameof(application));

        return SetTeamsApiClient(
            context,
            application.Options.LoggerFactory,
            ct);
    }

    /// <summary>
    /// Registers an ApiClient instance for Microsoft Teams API access in the current turn context.
    /// </summary>
    /// <remarks>After calling this method, the registered ApiClient can be retrieved from the
    /// context's service collection for use in subsequent Teams API operations.</remarks>
    /// <param name="context">The turn context in which to register the Teams ApiClient. Cannot be null.</param>
    /// <param name="loggerFactory">The logger factory used by the Teams API clients. Cannot be null.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the registration operation. Optional.</param>
    internal static Task SetTeamsApiClient(
        this ITurnContext context,
        ILoggerFactory loggerFactory,
        CancellationToken ct = default)
    {
        AssertionHelpers.ThrowIfNull(context, nameof(context));
        AssertionHelpers.ThrowIfNull(loggerFactory, nameof(loggerFactory));

        // The adapter creates these clients specifically for the current turn. Their REST transports
        // preserve all factory decisions, including named HttpClient configuration, regional endpoints,
        // and whether legacy, agentic-instance, or agentic-user authentication is required.
        var connectorTransport = GetRequiredRestTransport<IConnectorClient>(context);
        var userTokenTransport = GetRequiredRestTransport<IUserTokenClient>(context);

        ct.ThrowIfCancellationRequested();
        var connectorHttpClient = CreateLazyHttpClient(connectorTransport);
        var userTokenHttpClient = CreateLazyHttpClient(userTokenTransport);

        // teams.net separates conversation and user-token operations. Reuse the matching Agents SDK
        // transport for each so neither client reconstructs authentication or endpoint configuration.
        var conversationClient = new Microsoft.Teams.Core.ConversationClient(
            connectorHttpClient,
            loggerFactory.CreateLogger<Microsoft.Teams.Core.ConversationClient>());

        // teams.net builds absolute user-token request URLs from this setting rather than
        // HttpClient.BaseAddress, so propagate the transport endpoint for regional deployments.
        IConfiguration userTokenConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["UserTokenApiEndpoint"] = userTokenTransport.Endpoint.ToString()
            })
            .Build();
        var userTokenClient = new Microsoft.Teams.Core.UserTokenClient(
            userTokenHttpClient,
            userTokenConfiguration,
            loggerFactory.CreateLogger<Microsoft.Teams.Core.UserTokenClient>());

        var client = CreateApiClient(
            connectorTransport.Endpoint,
            connectorHttpClient,
            conversationClient,
            userTokenClient,
            loggerFactory.CreateLogger<Microsoft.Teams.Apps.Clients.ApiClient>());

        context.Services.Set<Microsoft.Teams.Apps.Clients.ApiClient>(client);
        return Task.CompletedTask;
    }

    internal static Microsoft.Teams.Apps.Clients.ApiClient GetTeamsApiClient(this ITurnContext context)
    {
        return context.Services.Get<Microsoft.Teams.Apps.Clients.ApiClient>();
    }

    private static IRestTransport GetRequiredRestTransport<TClient>(ITurnContext context)
        where TClient : class
    {
        var client = context.Services.Get<TClient>();
        if (client is not IRestTransport transport)
        {
            throw new InvalidOperationException(
                $"{typeof(TClient).Name} must be registered in ITurnContext.Services and implement {nameof(IRestTransport)}.");
        }
        if (transport.Endpoint == null)
        {
            throw new InvalidOperationException(
                $"{typeof(TClient).Name} must provide a non-null {nameof(IRestTransport.Endpoint)}.");
        }

        return transport;
    }

    private static Microsoft.Teams.Apps.Clients.ApiClient CreateApiClient(
        Uri serviceUrl,
        HttpClient httpClient,
        Microsoft.Teams.Core.ConversationClient conversationClient,
        Microsoft.Teams.Core.UserTokenClient userTokenClient,
        ILogger logger)
    {
        // Microsoft.Teams.Apps 2.1 documents this constructor but exposes it as internal.
        // The service-URL constructor initializes Conversations, Teams, and Meetings for this turn.
        var constructor = typeof(Microsoft.Teams.Apps.Clients.ApiClient).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(Uri),
                typeof(System.Net.Http.HttpClient),
                typeof(Microsoft.Teams.Core.ConversationClient),
                typeof(Microsoft.Teams.Core.UserTokenClient),
                typeof(ILogger),
                typeof(Microsoft.Teams.Core.Schema.AgenticIdentity)
            ],
            modifiers: null);

        if (constructor == null)
        {
            throw new MissingMethodException(
                typeof(Microsoft.Teams.Apps.Clients.ApiClient).FullName,
                ".ctor(Uri, HttpClient, ConversationClient, UserTokenClient, ILogger, AgenticIdentity)");
        }

        return (Microsoft.Teams.Apps.Clients.ApiClient)constructor.Invoke(
            [serviceUrl, httpClient, conversationClient, userTokenClient, logger, null]);
    }

    private static HttpClient CreateLazyHttpClient(IRestTransport transport)
    {
        return new HttpClient(new RestTransportHandler(transport))
        {
            BaseAddress = transport.Endpoint
        };
    }

    private sealed class RestTransportHandler(IRestTransport transport) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            using var forwardedRequest = await CloneRequestAsync(request, cancellationToken).ConfigureAwait(false);
            using var httpClient = await transport.GetHttpClientAsync().ConfigureAwait(false);
            return await httpClient.SendAsync(forwardedRequest, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<HttpRequestMessage> CloneRequestAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri)
            {
                Version = request.Version,
                VersionPolicy = request.VersionPolicy
            };

            foreach (var header in request.Headers)
            {
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            foreach (var option in request.Options)
            {
                clone.Options.Set(new HttpRequestOptionsKey<object>(option.Key), option.Value);
            }

            if (request.Content != null)
            {
                var content = new ByteArrayContent(
                    await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
                foreach (var header in request.Content.Headers)
                {
                    content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                clone.Content = content;
            }

            return clone;
        }
    }
}
