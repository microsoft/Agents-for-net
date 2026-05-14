// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Builder
{
    // Event ID registry for ChannelServiceAdapterBase log messages:
    //   1 = Turn response sent (LogTurnResponse)
    //   2 = ProcessProactive called (LogProcessProactive)
    //   3 = ProcessActivity called (LogProcessActivity)
    //   4 = Anonymous access warning (LogAnonymousAccess)
    //   Future messages: start from 5
    internal static partial class ChannelServiceAdapterLog
    {
#if !NETSTANDARD
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
            Message = "Turn Response: RequestId={RequestId}, Activity='{Activity}'")]
        internal static partial void LogTurnResponse(ILogger logger, string requestId, string activity);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
            Message = "ProcessProactive: Activity='{Activity}'")]
        internal static partial void LogProcessProactive(ILogger logger, string activity);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
            Message = "ProcessActivity: RequestId={RequestId}, Target={Agent}, Activity='{Activity}'")]
        internal static partial void LogProcessActivity(ILogger logger, string requestId, string agent, string activity);

        [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
            Message = "Anonymous access is enabled for channel: {ChannelId}.")]
        internal static partial void LogAnonymousAccess(ILogger logger, string channelId);
#else
        internal static void LogTurnResponse(ILogger logger, string requestId, string activity)
        {
            logger.LogDebug("Turn Response: RequestId={RequestId}, Activity='{Activity}'", requestId, activity);
        }

        internal static void LogProcessProactive(ILogger logger, string activity)
        {
            logger.LogDebug("ProcessProactive: Activity='{Activity}'", activity);
        }

        internal static void LogProcessActivity(ILogger logger, string requestId, string agent, string activity)
        {
            logger.LogDebug("ProcessActivity: RequestId={RequestId}, Target={Agent}, Activity='{Activity}'", requestId, agent, activity);
        }

        internal static void LogAnonymousAccess(ILogger logger, string channelId)
        {
            logger.LogWarning("Anonymous access is enabled for channel: {ChannelId}.", channelId);
        }
#endif
    }
}
