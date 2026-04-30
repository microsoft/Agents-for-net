// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using System;

namespace Microsoft.Agents.Extensions.Slack;

public static class SlackHelpers
{
    #region Extensions
    public static string SlackEncode(this string value)
    {
        // Encode text for Slack (https://api.slack.com/docs/message-formatting)
        return value?.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }

    public static string SlackDecode(this string value)
    {
        // Decode text for Slack (https://api.slack.com/docs/message-formatting)
        return value?.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
    }

    public static string GetSlackBotId(this ConversationAccount conversationAccount)
    {
        return SlackBotIdFromConversationId(conversationAccount.Id);
    }

    public static string GetSlackTeamId(this ConversationAccount conversationAccount)
    {
        return SlackTeamIdFromConversationId(conversationAccount.Id);
    }

    public static string GetSlackChannelId(this ConversationAccount conversationAccount)
    {
        return SlackChannelIdFromConversationId(conversationAccount.Id);
    }
    public static string GetSlackThreadTs(this ConversationAccount conversationAccount)
    {
        return SlackThreadTsFromConversationId(conversationAccount.Id);
    }
    #endregion

    public static string CreateConversationId(string slackBotId, string slackTeamId, string slackChannelId, string slackThreadTs)
    {
        if (slackThreadTs == null)
            return $"{slackBotId}:{slackTeamId}:{slackChannelId}";

        return $"{slackBotId}:{slackTeamId}:{slackChannelId}:{slackThreadTs}";
    }

    public static string SlackBotIdFromConversationId(string conversationId)
    {
        return FromConversationId(conversationId, 0);
    }

    public static string SlackTeamIdFromConversationId(string conversationId)
    {
        return FromConversationId(conversationId, 1);
    }

    public static string SlackChannelIdFromConversationId(string conversationId)
    {
        return FromConversationId(conversationId, 2);
    }

    public static string SlackThreadTsFromConversationId(string conversationId)
    {
        return FromConversationId(conversationId, 3);
    }

    private static string FromConversationId(string conversationId, int pos)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));

        var split = conversationId.Split(':');
        if (split.Length != 3 && split.Length != 4)
            throw new ArgumentException($"Invalid ConversationId: {conversationId}", nameof(conversationId));

        if (pos >= split.Length)
            return null;

        return split[pos];
    }
}
