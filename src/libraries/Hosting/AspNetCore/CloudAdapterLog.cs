// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    // Event ID registry for CloudAdapter log messages:
    //   1 = Turn begin (LogTurnBegin)
    //   2 = Turn response (LogTurnResponse)
    //   3 = Turn end (LogTurnEnd)
    //   4 = Activity accepted for background processing (LogActivityAccepted)
    //   5 = Processing exception (LogProcessingException)
    //   6 = Request cancelled (LogRequestCancelled)
    //   7 = Unexpected exception (LogUnexpectedException)
    //   Future messages: start from 8
    internal static partial class CloudAdapterLog
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
            Message = "Turn Begin (blocking): RequestId={RequestId}")]
        internal static partial void LogTurnBegin(ILogger logger, string requestId);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
            Message = "Turn Response (blocking): RequestId={RequestId}, Activity='{Activity}'")]
        internal static partial void LogTurnResponse(ILogger logger, string requestId, string activity);

        [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
            Message = "Turn End (blocking): RequestId={RequestId}, InvokeResponse='{InvokeResponse}'")]
        internal static partial void LogTurnEnd(ILogger logger, string requestId, string invokeResponse);

        [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
            Message = "Activity Accepted: RequestId={RequestId}, Activity='{Activity}'")]
        internal static partial void LogActivityAccepted(ILogger logger, string requestId, string activity);

        [LoggerMessage(EventId = 5, Level = LogLevel.Error,
            Message = "Exception processing activity for RequestId={RequestId}")]
        internal static partial void LogProcessingException(ILogger logger, Exception exception, string requestId);

        [LoggerMessage(EventId = 6, Level = LogLevel.Warning,
            Message = "CloudAdapter.ProcessAsync cancelled for RequestId={RequestId}")]
        internal static partial void LogRequestCancelled(ILogger logger, string requestId);

        [LoggerMessage(EventId = 7, Level = LogLevel.Error,
            Message = "Unexpected exception in CloudAdapter.ProcessAsync")]
        internal static partial void LogUnexpectedException(ILogger logger, Exception exception);
    }
}
