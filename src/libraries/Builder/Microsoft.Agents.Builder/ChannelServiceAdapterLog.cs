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
    //   5 = Anonymous access from trusted local transport (LogAnonymousAccessTrustedTransport)
    //   6 = Streaming response factory instantiation failed (LogStreamingResponseFactoryError)
    //   Future messages: start from 7
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

        [LoggerMessage(EventId = 5, Level = LogLevel.Information,
            Message = "Anonymous access is enabled for channel: {ChannelId} via trusted local transport.")]
        internal static partial void LogAnonymousAccessTrustedTransport(ILogger logger, string channelId);

        [LoggerMessage(EventId = 6, Level = LogLevel.Warning,
            Message = "Failed to create the streaming response factory for channel: {ChannelId}. Falling back to the default streaming response.")]
        internal static partial void LogStreamingResponseFactoryError(ILogger logger, System.Exception exception, string channelId);
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

        internal static void LogAnonymousAccessTrustedTransport(ILogger logger, string channelId)
        {
            logger.LogInformation("Anonymous access is enabled for channel: {ChannelId} via trusted local transport.", channelId);
        }

        internal static void LogStreamingResponseFactoryError(ILogger logger, System.Exception exception, string channelId)
        {
            logger.LogWarning(exception, "Failed to create the streaming response factory for channel: {ChannelId}. Falling back to the default streaming response.", channelId);
        }
#endif
    }
}
