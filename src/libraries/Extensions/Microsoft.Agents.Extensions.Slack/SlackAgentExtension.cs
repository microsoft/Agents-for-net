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

#pragma warning disable CA1822 // Mark members as static
    public Task<SlackResponse> CallAsync(ITurnContext turnContext, string method, object? options = null, string token = "", CancellationToken cancellationToken = default)
#pragma warning restore CA1822 // Mark members as static
    {
        return turnContext.Services.Get<SlackApi>().CallAsync(method, options, token, cancellationToken);
    }

#pragma warning disable CA1822 // Mark members as static
    public SlackStream CreateStream(ITurnContext turnContext)
#pragma warning restore CA1822 // Mark members as static
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();
        return new SlackStream(turnContext.Services.Get<SlackApi>(), channelData.EventEnvelope.event_content.channel, channelData.EventEnvelope.event_content.ts, channelData.ApiToken);
    }

    public SlackAgentExtension OnSlackMessage(RouteHandler routeHandler)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithSelector((context, ct) => Task.FromResult(context.Activity.IsType(ActivityTypes.Message) && context.Activity.ChannelId == ChannelId))
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .WithOrderRank(RouteRank.Last)
            .Build());
        return this;
    }

    public SlackAgentExtension OnSlackMessage(string text, RouteHandler routeHandler)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(text)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .Build());
        return this;
    }

    public SlackAgentExtension OnSlackMessage(Regex textPattern, RouteHandler routeHandler)
    {
        AgentApplication.AddRoute(MessageRouteBuilder.Create()
            .WithText(textPattern)
            .WithChannelId(ChannelId)
            .WithHandler(routeHandler)
            .Build());
        return this;
    }
}
