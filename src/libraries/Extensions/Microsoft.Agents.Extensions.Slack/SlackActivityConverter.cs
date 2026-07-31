// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;
using System;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.Agents.Extensions.Slack
{
    internal sealed class SlackActivityConverter
    {
        private readonly SlackAdapterOptions _options;

        internal SlackActivityConverter(SlackAdapterOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        internal SlackActivity? Convert(ParsedSlackRequest request, Type agentType)
        {
            var agentName = agentType.GetCustomAttribute<AgentAttribute>()?.Name ?? agentType.Name;
            return request.Kind switch
            {
                SlackRequestKind.Event => ConvertEvent(request.EventEnvelope!, agentName),
                SlackRequestKind.Interactive => ConvertInteractive(request.ActionPayload!, agentName),
                _ => null,
            };
        }

        private SlackActivity? ConvertEvent(EventEnvelope envelope, string agentName)
        {
            var content = envelope.event_content;
            if (content == null)
            {
                return null;
            }

            var botId = content.Get<string>("bot_id");
            var nestedBotId = content.Get<string>("message.bot_id");
            var nestedUserId = content.Get<string>("message.user");
            if (!string.IsNullOrEmpty(botId)
                || !string.IsNullOrEmpty(nestedBotId)
                || (!string.IsNullOrEmpty(_options.BotUserId)
                    && (string.Equals(content.user, _options.BotUserId, StringComparison.Ordinal)
                        || string.Equals(nestedUserId, _options.BotUserId, StringComparison.Ordinal))))
            {
                return null;
            }

            var teamId = ResolveEventTeamId(envelope);
            var channel = content.channel;
            var threadTs = content.Get<string>("thread_ts");

            var activity = new SlackActivity
            {
                ChannelId = Channels.Slack,
                ServiceUrl = SlackAdapter.SlackServiceUrl,
                Id = envelope.event_id ?? content.ts,
                Timestamp = DateTimeOffset.UtcNow,
                From = new ChannelAccount(id: SlackHelpers.CreateAccountId(content.user, teamId)),
                Recipient = new ChannelAccount(
                    id: SlackHelpers.CreateAccountId(_options.BotId, teamId),
                    name: agentName),
                Conversation = new ConversationAccount(
                    id: SlackHelpers.CreateConversationId(_options.BotId, teamId, channel, threadTs))
                {
                    IsGroup = !string.Equals(content.channel_type, "im", StringComparison.Ordinal),
                },
                ChannelData = new SlackChannelData
                {
                    Envelope = envelope,
                    ApiToken = _options.BotToken,
                },
            };

            if (string.Equals(content.type, "message", StringComparison.Ordinal) && string.IsNullOrEmpty(content.subtype))
            {
                activity.Type = ActivityTypes.Message;
                activity.Text = content.text.SlackDecode();
            }
            else
            {
                activity.Type = ActivityTypes.Event;
                activity.Name = content.type;
            }

            return activity;
        }

        private SlackActivity ConvertInteractive(ActionPayload payload, string agentName)
        {
            var teamId = ResolveInteractiveTeamId(payload);
            var channel = payload.channel;
            var user = payload.Get<string>("user.id");
            var threadTs = payload.Get<string>("message.thread_ts") ?? payload.Get<string>("message.ts");

            var activity = new SlackActivity
            {
                Type = ActivityTypes.Event,
                Name = payload.type,
                ChannelId = Channels.Slack,
                ServiceUrl = SlackAdapter.SlackServiceUrl,
                Id = Guid.NewGuid().ToString(),
                Timestamp = DateTimeOffset.UtcNow,
                From = new ChannelAccount(id: SlackHelpers.CreateAccountId(user, teamId)),
                Recipient = new ChannelAccount(
                    id: SlackHelpers.CreateAccountId(_options.BotId, teamId),
                    name: agentName),
                Conversation = new ConversationAccount(
                    id: SlackHelpers.CreateConversationId(_options.BotId, teamId, channel, threadTs)),
                ChannelData = new SlackChannelData
                {
                    Payload = payload,
                    ApiToken = _options.BotToken,
                },
            };

            var actionType = payload.Get<string>("actions[0].type");
            if ((string.Equals(payload.type, "interactive_message", StringComparison.Ordinal)
                    || string.Equals(payload.type, "block_actions", StringComparison.Ordinal))
                && string.Equals(actionType, "feedback_buttons", StringComparison.Ordinal))
            {
                activity.Type = ActivityTypes.Invoke;
                activity.Name = "message/submitAction";
                activity.ReplyToId = threadTs;
                activity.Value = new
                {
                    actionName = payload.Get<string>("actions[0].action_id"),
                    actionValue = new
                    {
                        reaction = payload.Get<string>("actions[0].value").SlackDecode(),
                    },
                    replyToId = threadTs,
                };
            }
            else if ((string.Equals(payload.type, "interactive_message", StringComparison.Ordinal)
                    || string.Equals(payload.type, "block_actions", StringComparison.Ordinal))
                && (string.Equals(actionType, "select", StringComparison.Ordinal)
                    || string.Equals(actionType, "button", StringComparison.Ordinal)))
            {
                activity.Type = ActivityTypes.Message;
                activity.Name = null;
                activity.Text = (string.Equals(actionType, "select", StringComparison.Ordinal)
                    ? payload.Get<string>("actions[0].selected_options[0].value")
                    : payload.Get<string>("actions[0].value")).SlackDecode();
                activity.Entities =
                [
                    new Mention
                    {
                        Mentioned = activity.Recipient,
                        Text = $"@{activity.Recipient.Name}",
                    },
                ];
            }
            else if (string.Equals(payload.type, "message_action", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(payload.Get<string>("callback_id")))
            {
                activity.Type = ActivityTypes.Event;
                activity.Name = "SlackActivity";
                activity.Value = payload.Get<string>("callback_id");
            }
            else
            {
                activity.Type = ActivityTypes.Event;
                activity.Name = $"vnd.slack.action.{payload.type}";
                activity.Value = JsonSerializer.SerializeToNode(
                    payload,
                    ProtocolJsonSerializer.SerializationOptions);
            }

            return activity;
        }

        private static string ResolveEventTeamId(EventEnvelope envelope)
        {
            var teamId = !string.IsNullOrWhiteSpace(envelope.team_id)
                ? envelope.team_id
                : !string.IsNullOrWhiteSpace(envelope.context_team_id)
                    ? envelope.context_team_id
                    : envelope.event_content?.team;

            if (string.IsNullOrWhiteSpace(teamId))
            {
                throw new JsonException("Slack event payload does not contain a team ID.");
            }

            return teamId;
        }

        private static string ResolveInteractiveTeamId(ActionPayload payload)
        {
            var teamId = payload.Get<string>("team.id")
                ?? payload.Get<string>("user.team_id")
                ?? payload.Get<string>("view.app_installed_team_id");

            if (string.IsNullOrWhiteSpace(teamId))
            {
                throw new JsonException("Slack interactivity payload does not contain a team ID.");
            }

            return teamId;
        }
    }
}
