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

    public SlackAgentExtension OnSlackMessage(RouteHandler routeHandler, string[] autoSigninHandlers = null, ushort rank = RouteRank.Unspecified)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithSelector((context, ct) => Task.FromResult(context.Activity.IsType(ActivityTypes.Message) && context.Activity.ChannelId == ChannelId))
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(rank)
            .WithOAuthHandlers(autoSigninHandlers)
            .Build());
        return this;
    }

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
}
