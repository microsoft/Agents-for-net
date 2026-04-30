// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack;

/// <summary>
/// Provides Slack-specific extensions for an agent application, enabling message and event routing, Slack API calls,
/// and stream management for the Slack channel.
/// </summary>
/// <remarks>
/// <para>
/// Use this extension to integrate Slack channel support into an agent application. It offers methods to
/// register message and event handlers that are scoped to Slack, as well as utilities for interacting with the Slack
/// API and managing Slack conversation streams. All routes and handlers registered through this extension are limited
/// to activities originating from the Slack channel.
/// </para>
/// <para>
/// The preferred way to enable the Slack extension is via the <see cref="SlackExtensionAttribute"/> on a
/// <c>partial</c> <see cref="AgentApplication"/> subclass, which causes a source generator to expose a
/// <c>SlackExtension</c> property of this type automatically.
/// Use this constructor directly only when manually calling
/// <see cref="AgentApplication.RegisterExtension(IAgentExtension)"/>.
/// </para>
/// </remarks>
public class SlackAgentExtension : AgentExtension
{
    private static readonly Task<bool> _completedTrue = Task.FromResult(true);

#if !NETSTANDARD
    protected AgentApplication AgentApplication { get; init; }
#else
    protected AgentApplication AgentApplication { get; set;}
#endif

    public SlackAgentExtension(AgentApplication application)
    {
        ChannelId = Channels.Slack;
        AgentApplication = application;

        var slackApi = new SlackApi(application.Options.HttpClientFactory);
        application.OnBeforeTurn((turnContext, turnState, cancellationToken) =>
        {
            if (turnContext.Activity.ChannelId == ChannelId)
            {
                turnContext.Services.Set(slackApi);
            }
            return _completedTrue;
        });
    }

    /// <summary>
    /// Invokes a Slack Web API method asynchronously using the specified context and parameters.
    /// </summary>
    /// <param name="turnContext">The turn context that provides access to the Slack API service and conversation state. Cannot be null.</param>
    /// <param name="method">The name of the Slack Web API method to call. Must be a valid Slack API method name.</param>
    /// <param name="options">An optional object containing parameters to include in the API request. May be null if no additional options are
    /// required.</param>
    /// <param name="token">An optional Slack authentication token to use for the API call. If empty, the request is sent without
    /// an Authorization header.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the response from the Slack API.</returns>
    public Task<SlackResponse> CallAsync(ITurnContext turnContext, string method, object? options = null, string token = "", CancellationToken cancellationToken = default)
    {
        return turnContext.Services.Get<SlackApi>().CallAsync(method, options, token, cancellationToken);
    }

    /// <summary>
    /// Creates and starts a new Slack stream for the specified conversation or thread.
    /// </summary>
    /// <param name="turnContext">The turn context containing the current activity and service references. Cannot be null.</param>
    /// <param name="thread_ts">The thread timestamp identifying the Slack thread to join. If null, the value from "event.ts" is used.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the started Slack stream.</returns>
    public Task<SlackStream> CreateStreamAsync(ITurnContext turnContext, string? thread_ts = null)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();
        var stream = new SlackStream(turnContext.Services.Get<SlackApi>(), channelData.Envelope.event_content.channel, thread_ts ?? channelData.Envelope.event_content.ts, channelData.ApiToken);
        return stream.StartAsync();
    }

    /// <summary>
    /// Registers a message route handler for any Slack message received by the agent.
    /// </summary>
    /// <param name="routeHandler">The delegate that processes incoming Slack message activities. This handler will be invoked when a message
    /// activity is received on the Slack channel.</param>
    /// <param name="autoSigninHandlers">An optional array of handler names that support automatic sign-in. If specified, these handlers will be used to
    /// facilitate OAuth flows for the route.</param>
    /// <param name="rank">The order rank that determines the priority of the route. Use RouteRank.Unspecified to assign the default rank.</param>
    /// <returns>The current instance of SlackAgentExtension to allow method chaining.</returns>
    public SlackAgentExtension OnMessage(RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.Message)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers a message route that triggers the specified handler when an incoming Slack message matches the given
    /// text.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnMessage in that this only matches for the slack channel.</remarks>
    /// <param name="text">The text pattern to match incoming Slack messages. The route is triggered when a message matches this text.</param>
    /// <param name="routeHandler">The handler to invoke when the route is matched. Responsible for processing the incoming message.</param>
    /// <param name="autoSigninHandlers">An optional array of OAuth handler names to use for automatic sign-in. If null, no auto sign-in handlers are
    /// applied.</param>
    /// <param name="rank">The rank that determines the order in which this route is evaluated. Use RouteRank.Unspecified for default
    /// ordering.</param>
    /// <returns>The current instance of SlackAgentExtension to allow method chaining.</returns>
    public SlackAgentExtension OnMessage(string text, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(text)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers a message route that triggers the specified handler when an incoming Slack message matches the given
    /// text pattern.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnMessage in that this only matches for the slack channel.</remarks>
    /// <param name="textPattern">A regular expression used to match the text of incoming Slack messages. The route is triggered when the message
    /// text matches this pattern.</param>
    /// <param name="routeHandler">The handler to invoke when the route is matched. This delegate processes the incoming message.</param>
    /// <param name="autoSigninHandlers">An optional array of OAuth handler names to use for automatic sign-in if authentication is required. May be null
    /// if no auto sign-in is needed.</param>
    /// <param name="rank">The rank that determines the order in which this route is evaluated relative to other routes. Lower values
    /// indicate higher priority. The default is RouteRank.Unspecified.</param>
    /// <returns>The current instance of SlackAgentExtension to allow method chaining.</returns>
    public SlackAgentExtension OnMessage(Regex textPattern, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(textPattern)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers a message route handler for any Slack event received by the agent.
    /// </summary>
    /// <param name="routeHandler">The delegate that processes incoming Slack event activities. This handler will be invoked when an event
    /// activity is received on the Slack channel.</param>
    /// <param name="autoSigninHandlers">An optional array of handler names that support automatic sign-in. If specified, these handlers will be used to
    /// facilitate OAuth flows for the route.</param>
    /// <param name="rank">The order rank that determines the priority of the route. Use RouteRank.Unspecified to assign the default rank.</param>
    /// <returns>The current instance of SlackAgentExtension to allow method chaining.</returns>
    public SlackAgentExtension OnEvent(RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(TypeRouteBuilder.Create()
            .WithType(ActivityTypes.Event)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank == RouteRank.Unspecified ? RouteRank.Last : rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers an event route that triggers the specified handler when an incoming Slack event matches the given
    /// name.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnEvent in that this only matches for the slack channel.</remarks>
    /// <param name="eventName">The name of the Slack event to handle. This value identifies the event type that triggers the route.</param>
    /// <param name="routeHandler">The delegate that processes incoming Slack event activities. This handler will be invoked when an event
    /// activity is received on the Slack channel.</param>
    /// <param name="autoSigninHandlers">An optional array of handler names that support automatic sign-in. If specified, these handlers will be used to
    /// facilitate OAuth flows for the route.</param>
    /// <param name="rank">The order rank that determines the priority of the route. Use RouteRank.Unspecified to assign the default rank.</param>
    /// <returns>The current instance of SlackAgentExtension to allow method chaining.</returns>
    public SlackAgentExtension OnEvent(string eventName, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(EventRouteBuilder.Create()
            .WithName(eventName)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

    /// <summary>
    /// Registers an event route that triggers the specified handler when an incoming Slack event matches the given
    /// name pattern.
    /// </summary>
    /// <remarks>This differs from AgentApplication.OnEvent in that this only matches for the slack channel.</remarks>
    /// <param name="eventNamePattern">The regular expression pattern that matches the name of the Slack event to handle. This value identifies the event type that triggers the route.</param>
    /// <param name="routeHandler">The delegate that processes incoming Slack event activities. This handler will be invoked when an event
    /// activity is received on the Slack channel.</param>
    /// <param name="autoSigninHandlers">An optional array of handler names that support automatic sign-in. If specified, these handlers will be used to
    /// facilitate OAuth flows for the route.</param>
    /// <param name="rank">The order rank that determines the priority of the route. Use RouteRank.Unspecified to assign the default rank.</param>
    /// <returns>The current instance of SlackAgentExtension to allow method chaining.</returns>
    public SlackAgentExtension OnEvent(Regex eventNamePattern, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(EventRouteBuilder.Create()
            .WithName(eventNamePattern)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }
}
