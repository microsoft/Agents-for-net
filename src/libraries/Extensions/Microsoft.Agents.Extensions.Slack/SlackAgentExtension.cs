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

public class SlackAgentExtension : AgentExtension
{
#if !NETSTANDARD
    protected AgentApplication AgentApplication { get; init; }
#else
    protected AgentApplication AgentApplication { get; set;}
#endif

    public SlackAgentExtension(AgentApplication application) 
    {
        ChannelId = Channels.Slack;
        AgentApplication = application;

        application.OnBeforeTurn((turnContext, turnState, cancellationToken) =>
        {
            if (turnContext.Activity.ChannelId == ChannelId)
            {
                var slackApi = new SlackApi(application.Options.HttpClientFactory);
                turnContext.Services.Set(slackApi);
            }
            return Task.FromResult(true);
        });
    }

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
        var stream = new SlackStream(turnContext.Services.Get<SlackApi>(), channelData.EventEnvelope.event_content.channel, thread_ts ?? channelData.EventEnvelope.event_content.ts, channelData.ApiToken);
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
    public SlackAgentExtension OnSlackMessage(RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
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
    public SlackAgentExtension OnSlackMessage(string text, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
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
    public SlackAgentExtension OnSlackMessage(Regex textPattern, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
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
    public SlackAgentExtension OnSlackEvent(RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
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
    public SlackAgentExtension OnSlackEvent(string eventName, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
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
    public SlackAgentExtension OnSlackEvent(Regex eventNamePattern, RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
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
