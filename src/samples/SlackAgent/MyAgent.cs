// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack;
using Microsoft.Agents.Extensions.Slack.Api;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SlackAgent;

[Agent(name: "MyAgent", description: "Echo user messages back on slack", version: "1.0")]
[SlackExtension]
public partial class MyAgent : AgentApplication
{
    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        SlackExtension.OnMessage("-stream", OnSlackStreamMessageAsync);
        SlackExtension.OnMessage(OnSlackMessageAsync, rank: RouteRank.Last);
        OnConversationUpdate(ConversationUpdateEvents.MembersAdded, WelcomeMessageAsync);
    }

    private async Task WelcomeMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (ChannelAccount member in turnContext.Activity.MembersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            }
        }
    }

    private async Task OnSlackMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var channelData = turnContext.Activity.GetChannelData<SlackChannelData>();

        var message = $$"""
        {
            "channel": "{{channelData.EventEnvelope?.Get<string>("event.channel")}}",
            "text": "You said: {{turnContext.Activity.Text}}"
        }
        """;

        await SlackExtension.CallAsync(turnContext, "chat.postMessage", message, channelData.ApiToken, cancellationToken);
    }

    private async Task OnSlackStreamMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var stream = await SlackExtension.CreateStreamAsync(turnContext);

        try
        {
            await stream.AppendAsync(new TaskUpdateChunk(id: "task1", title: "Working it", status: SlackTaskStatus.InProgress));
            await Task.Delay(2000, cancellationToken);

            await stream.AppendAsync(markdown_text: "This ");
            await Task.Delay(1500, cancellationToken);

            await stream.AppendAsync([
                    new MarkdownTextChunk("is "),
                    new TaskUpdateChunk(id: "task1", title: "Still working it", status: SlackTaskStatus.InProgress)
                ]);
            await Task.Delay(1500, cancellationToken);

            await stream.AppendAsync(markdown_text: "a ");
            await Task.Delay(1500, cancellationToken);

            await stream.AppendAsync(markdown_text: "test.");

            await stream.AppendAsync(new TaskUpdateChunk(id: "task1", title: "Done", status: SlackTaskStatus.Complete));
        }
        catch (Exception)
        {
            await stream.AppendAsync(new TaskUpdateChunk(id: "task1", title: "Error", status: SlackTaskStatus.Error));
        }
        finally
        {
            var feedbackButtons = """
            {
                "blocks": 
                [
                    {
                        "type": "context_actions",
                        "elements": [
                            {
                                "type": "feedback_buttons",
                                "action_id": "feedback",
                                "positive_button": {
                                    "text": {
                                        "type": "plain_text",
                                        "text": "👍"
                                    },
                                    "value": "positive_feedback"
                                },
                                "negative_button": {
                                    "text": {
                                        "type": "plain_text",
                                        "text": "👎"
                                    },
                                    "value": "negative_feedback"
                                }
                            }
                        ]
                    }
                ]
            }
            """;

            // Legacy: https://docs.slack.dev/legacy/legacy-messaging/legacy-message-buttons/
            // New: Feedback buttons: https://docs.slack.dev/reference/block-kit/blocks/context-actions-block
            await stream.StopAsync(blocks: feedbackButtons);
        }
    }
}