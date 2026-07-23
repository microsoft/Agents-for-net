// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack;
using Microsoft.Agents.Extensions.Slack.Api;
using System.Threading;
using System.Threading.Tasks;

namespace SlackAgent;

[Agent(name: "MyAgent", description: "Demonstrates slack functionality", version: "1.0")]
[SlackExtension]
public partial class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [SlackMembersAddedRoute]
    public async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    // Demonstrates using the Slack API to reply to a message with the text "You said: {message text}" instead of
    // the typical ITurnContext.SendActivityAsync response.
    [SlackMessageRoute]
    public async Task OnSlackMessageAsync(ITurnContext<ISlackActivity> turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.ChannelData;

        var message = $$"""
        {
            "channel": "{{channelData.Channel}}",
            "text": "You said: {{turnContext.Activity.Text}}",
            "thread_ts": "{{channelData.ThreadTs}}"
        }
        """;

        await SlackExtension.CallAsync(turnContext, "chat.postMessage", message, channelData.ApiToken, cancellationToken);
    }
    
    [SlackEventRoute]
    public async Task OnSlackEventAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();

        var message = $$"""
        {
            "channel": "{{channelData.Channel}}",
            "text": "Agent got: {{turnContext.Activity.Name}}"
        }
        """;

        await SlackExtension.CallAsync(turnContext, "chat.postMessage", message, channelData.ApiToken, cancellationToken);
    }
    
    [SlackMessageRoute("-buttons")]
    public async Task OnSlackButtonsAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();
        var buttons = $$"""
        {
            "channel": "{{channelData.Channel}}",
            "thread_ts": "{{channelData.ThreadTs}}",
            "blocks": [
                {
                    "type": "section",
                    "text": { "type": "mrkdwn", "text": "Pick an option:" },
                },
                {
                    "type": "actions",
                    "elements": [
                        {
                            "type": "button",
                            "text": { "type": "plain_text", "text": "Yes" },
                            "action_id": "button_yes",
                            "value": "yes",
                        },
                        {
                            "type": "button",
                            "text": { "type": "plain_text", "text": "No" },
                            "action_id": "button_no",
                            "value": "no",
                        },
                    ],
                },
            ],
        }
        """;

        await SlackExtension.CallAsync(turnContext, "chat.postMessage", buttons, channelData.ApiToken, cancellationToken);
    }

    [SlackMessageRoute("-stream")]
    public async Task OnSlackStreamMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        // ITurnContext.StreamingResponse resolves to the Slack streaming implementation for Slack turns,
        // which streams via chat.startStream / chat.appendStream / chat.stopStream under the covers.
        var stream = turnContext.StreamingResponse;
        stream.FeedbackLoopEnabled = true;

        await stream.QueueInformativeUpdateAsync("Working it", cancellationToken);

        foreach (var word in new[] { "This ", "is ", "a ", "test." })
        {
            stream.QueueTextChunk(word);
            await Task.Delay(1500, cancellationToken);
        }

        await stream.EndStreamAsync(cancellationToken);
    }

    [SlackFeedbackLoopRoute]
    public async Task OnSlackFeedbackLoopAsync(ITurnContext turnContext, ITurnState turnState, FeedbackData feedbackData, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();
        var message = $$"""
        {
            "channel": "{{channelData.Channel}}",
            "text": "Agent got feedback: {{feedbackData?.ActionValue?.Reaction}}",
            "thread_ts": "{{channelData.ThreadTs}}"
        }
        """;
        await SlackExtension.CallAsync(turnContext, "chat.postMessage", message, channelData.ApiToken, cancellationToken);
    }
}