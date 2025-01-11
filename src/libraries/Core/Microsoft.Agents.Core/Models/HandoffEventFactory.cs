// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Interfaces;
using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Contains utility methods for creating various event types.
    /// </summary>
    public static class HandoffEventFactory
    {
        /// <summary>
        /// Create handoff initiation event.
        /// </summary>
        /// <param name="turnContext">turn context.</param>
        /// <param name="handoffContext">agent hub-specific context.</param>
        /// <param name="transcript">transcript of the conversation.</param>
        /// <returns>handoff event.</returns>
        public static Activity CreateHandoffInitiation(ITurnContext turnContext, object handoffContext, Transcript transcript = null)
        {
            ArgumentNullException.ThrowIfNull(turnContext);

            var handoffEvent = CreateHandoffEvent(HandoffEventNames.InitiateHandoff, handoffContext, turnContext.Activity.Conversation);

            handoffEvent.From = turnContext.Activity.From;
            handoffEvent.RelatesTo = turnContext.Activity.GetConversationReference();
            handoffEvent.ReplyToId = turnContext.Activity.Id;
            handoffEvent.ServiceUrl = turnContext.Activity.ServiceUrl;
            handoffEvent.ChannelId = turnContext.Activity.ChannelId;

            if (transcript != null)
            {
                var attachment = new Attachment
                {
                    Content = transcript,
                    ContentType = "application/json",
                    Name = "Transcript",
                };
                handoffEvent.Attachments.Add(attachment);
            }

            return handoffEvent;
        }

        /// <summary>
        /// Create handoff status event.
        /// </summary>
        /// <param name="conversation">Conversation being handed over.</param>
        /// <param name="state">State, possible values are: "accepted", "failed", "completed".</param>
        /// <param name="message">Additional message for failed handoff.</param>
        /// <returns>handoff event.</returns>
        public static Activity CreateHandoffStatus(ConversationAccount conversation, string state, string message = null)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentException.ThrowIfNullOrWhiteSpace(state);

            object value = new { state, message };

            var handoffEvent = CreateHandoffEvent(HandoffEventNames.HandoffStatus, value, conversation);
            return handoffEvent;
        }

        private static Activity CreateHandoffEvent(string name, object value, ConversationAccount conversation)
        {
            var handoffEvent = Activity.CreateEventActivity() as Activity;

            handoffEvent.Name = name;
            handoffEvent.Value = value;
            handoffEvent.Id = Guid.NewGuid().ToString();
            handoffEvent.Timestamp = DateTime.UtcNow;
            handoffEvent.Conversation = conversation;
            handoffEvent.Attachments = new List<Attachment>();
            handoffEvent.Entities = new List<Entity>();
            return handoffEvent;
        }
    }
}
