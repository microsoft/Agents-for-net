// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;
using System.Linq;

namespace Microsoft.Agents.Core.Validation
{
    /// <summary>
    /// Perform Activity validation.
    /// </summary>
    /// <remarks>
    /// This is currently limited to validation of incoming Activities from a Channel.  Long term
    /// there will be expanded, rules-based, validation for all contexts.
    /// </remarks>
    public static class ActivityValidation
    {
        /// <summary>
        /// Valid Activity
        /// </summary>
        /// <param name="activity"></param>
        /// <param name="context">Array of <see cref="ValidationContext"/></param>
        /// <returns></returns>
        public static bool Validate(this IActivity activity, string[] context)
        {
            // For now, until there is rules based validation, perform in-code validation
            var isValid = AllValidation(activity);

            if (context.Contains(ValidationContext.Channel))
            {
                isValid &= ChannelValidation(activity);
            }

            if (context.Contains(ValidationContext.Receiver))
            {
                isValid &= ReceiverValidation(activity);
            }

            return isValid;
        }

        private static bool IsNamedType(this IActivity activity)
        {
            return activity.IsType(ActivityTypes.Invoke) || activity.IsType(ActivityTypes.Event) || activity.IsType(ActivityTypes.Command) || activity.IsType(ActivityTypes.CommandResult);
        }

        private static bool AllValidation(IActivity activity)
        {
            if (string.IsNullOrEmpty(activity.Type?.ToString()))
            {
                System.Diagnostics.Trace.WriteLine("A2010: Activities MUST include a type field");
                return false;
            }

            if (string.IsNullOrEmpty(activity.Conversation?.Id))
            {
                System.Diagnostics.Trace.WriteLine("A2080: Channels, Agents, and Clients MUST include the conversation and conversation.id fields");
                return false;
            }

            if (activity.IsNamedType() && string.IsNullOrEmpty(activity.Name))
            {
                System.Diagnostics.Trace.WriteLine("A5001,A5401,A6310,A6411: Event, Invoke, Command, and CommandResult activities MUST contain a `name` field");
                return false;
            }

            return true;
        }

        private static bool ChannelValidation(IActivity activity)
        {
            if (string.IsNullOrEmpty(activity.ChannelId?.ToString()))
            {
                System.Diagnostics.Trace.WriteLine("A2020: Channel Activities MUST include a `channelId` field, with string value type");
                return false;
            }

            return true;
        }

        private static bool ReceiverValidation(IActivity activity)
        {
            return true;
        }
    }
}
