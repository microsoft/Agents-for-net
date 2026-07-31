// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Extensions.Slack;

internal static partial class SlackAdapterLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Slack payload received: RequestId={RequestId}, Payload='{Payload}'")]
    internal static partial void LogPayloadReceived(ILogger logger, string requestId, string payload);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Slack Activity created: RequestId={RequestId}, ConversationId={ConversationId}, Activity='{Activity}'")]
    internal static partial void LogActivityCreated(
        ILogger logger,
        string requestId,
        string conversationId,
        string activity);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Slack message sent: ConversationId={ConversationId}, SlackTimestamp={SlackTimestamp}, Activity='{Activity}'")]
    internal static partial void LogMessageSent(
        ILogger logger,
        string conversationId,
        string slackTimestamp,
        string activity);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Slack message updated: ConversationId={ConversationId}, SlackTimestamp={SlackTimestamp}, Activity='{Activity}'")]
    internal static partial void LogMessageUpdated(
        ILogger logger,
        string conversationId,
        string slackTimestamp,
        string activity);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug,
        Message = "Slack message deleted: ConversationId={ConversationId}, SlackTimestamp={SlackTimestamp}, Reference='{Reference}'")]
    internal static partial void LogMessageDeleted(
        ILogger logger,
        string conversationId,
        string slackTimestamp,
        string reference);
}
