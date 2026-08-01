// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Extensions.Slack.Api;

internal static partial class SlackApiLog
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Slack API request: Method={Method}, Options='{Options}'")]
    internal static partial void LogRequest(ILogger logger, string method, string options);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
        Message = "Slack API response: Method={Method}, StatusCode={StatusCode}, Body='{Body}'")]
    internal static partial void LogResponse(ILogger logger, string method, int statusCode, string body);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "Slack external upload request: ContentLength={ContentLength}")]
    internal static partial void LogExternalUploadRequest(ILogger logger, int contentLength);

    [LoggerMessage(EventId = 4, Level = LogLevel.Debug,
        Message = "Slack external upload response: StatusCode={StatusCode}")]
    internal static partial void LogExternalUploadResponse(ILogger logger, int statusCode);
}
