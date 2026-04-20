// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack;
using Microsoft.Agents.Extensions.Slack.Api;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SlackAgent;

//[SlackExtension] // Uncomment this line once the Teams Extension changes are merged.
[Agent(name: "MyAgent", description: "Echo user messages back on slack", version: "1.0")]
public class MyAgent : AgentApplication
{
    public SlackAgentExtension SlackExtension { get; private set; }

    public MyAgent(AgentApplicationOptions options) : base(options)
    {
        // Remove these lines and uncomment the [SlackExtension] attribute on this class once the Teams Extension changes are merged.
        SlackExtension = new SlackAgentExtension(this);
        RegisterExtension(SlackExtension, slack =>
        {
            slack.OnSlackMessage("-stream", OnSlackStreamMessageAsync);
            slack.OnSlackMessage(OnSlackMessageAsync, rank: RouteRank.Last);
        });

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

        var message = new
        {
            channel = channelData.EventEnvelope?.Get<string>("event.channel"),
            text = $"You said: {turnContext.Activity.Text}"
        };

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
            // Legacy: https://docs.slack.dev/legacy/legacy-messaging/legacy-message-buttons/
            // New: Feedback buttons: https://docs.slack.dev/reference/block-kit/blocks/context-actions-block
            await stream.StopAsync(blocks: [
                new
                {
                    type = "context_actions",
                    elements = new List<object>
                    {
                        new
                        {
                            type = "feedback_buttons",
                            action_id = "feedback",
                            positive_button = new
                            {
                                text = new {
                                    type = "plain_text",
                                    text = "👍",
                                },
                                value = "good-feedback"
                            },
                            negative_button = new
                            {
                                text = new {
                                    type = "plain_text",
                                    text = "👎",
                                },
                                value = "bad-feedback"
                            }
                        }
                    }
                }
            ]);
        }
    }
}