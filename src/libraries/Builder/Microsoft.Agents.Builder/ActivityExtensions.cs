// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder
{
    public static class ActivityExtensions
    {
        public static bool IsAgenticRequest(this IActivity activity)
        {
            return activity?.Recipient?.Role == RoleTypes.AgenticIdentity
                || activity?.Recipient?.Role == RoleTypes.AgenticUser;
        }

        public static string GetAgentInstanceId(this IActivity activity)
        {
            if (!activity.IsAgenticRequest()) return null;
            return activity?.Recipient?.AgenticAppId;
        }

        public static string GetAgenticUser(this IActivity activity)
        {
            if (!activity.IsAgenticRequest()) return null;
            return activity?.Recipient?.AgenticUserId;
        }
    }
}
