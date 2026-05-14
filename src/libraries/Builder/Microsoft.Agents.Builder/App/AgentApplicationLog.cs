// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Builder.App
{
    // Event ID registry for AgentApplication log messages:
    //   1 = Route evaluation order (LogRouteList)
    //   2 = No route matched for activity type (LogNoRouteMatched)
    //   Future messages: start from 3
    public partial class AgentApplication
    {
#if !NETSTANDARD
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
            Message = "AgentApplication route evaluation order ({RouteCount} routes): {RouteList}")]
        private static partial void LogRouteList(ILogger logger, int routeCount, string routeList);

        [LoggerMessage(EventId = 2, Level = LogLevel.Debug,
            Message = "No route matched for ActivityType={ActivityType}")]
        private static partial void LogNoRouteMatched(ILogger logger, string activityType);
#else
        private static void LogRouteList(ILogger logger, int routeCount, string routeList)
        {
            logger.LogDebug("AgentApplication route evaluation order ({RouteCount} routes): {RouteList}",
                routeCount, routeList);
        }

        private static void LogNoRouteMatched(ILogger logger, string activityType)
        {
            logger.LogDebug("No route matched for ActivityType={ActivityType}", activityType);
        }
#endif
    }
}
