// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models.Entities;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Public Extensions for <see cref="IActivity"/>. type
    /// </summary>
    public static class IActivityExtensions
    {
        /// <summary>
        /// Converts an <see cref="IActivity"/> to a JSON string.
        /// </summary>
        /// <param name="activity">Activity to convert to Json Payload</param>
        /// <returns>JSON String</returns>
        public static string ToJson(this IActivity activity)
        {
            return ProtocolJsonSerializer.ToJson(activity);
        }

        /// <summary>
        /// Resolves the mentions from the entities of this activity.
        /// </summary>
        /// <returns>The array of mentions; or an empty array, if none are found.</returns>
        /// <remarks>This method is defined on the <see cref="Activity"/> class, but is only intended
        /// for use with a message activity, where the activity <see cref="Activity.Type"/> is set to
        /// <see cref="ActivityTypes.Message"/>.</remarks>
        /// <seealso cref="Mention"/>
        public static Mention[] GetMentions(this IActivity activity)
        {
            var result = new List<Mention>();
            if (activity.Entities != null)
            {
                foreach (var entity in activity.Entities)
                {
                    if (entity is Mention mention)
                    {
                        result.Add(mention);
                    }
                }
            }
            return [.. result];
        }

        /// <summary>
        /// Clone the activity to a new instance of activity. 
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public static IActivity Clone(this IActivity activity)
        {
            return ProtocolJsonSerializer.CloneTo<IActivity>(activity);
        }

        /// <summary>
        /// Gets the channel data for this activity as a strongly-typed object.
        /// </summary>
        /// <typeparam name="T">The type of the object to return.</typeparam>
        /// <returns>The strongly-typed object; or the type's default value, if the ChannelData is null.</returns>
        public static T GetChannelData<T>(this IActivity activity)
        {
            if (activity.ChannelData == null)
            {
                return default;
            }

            if (activity.ChannelData.GetType() == typeof(T))
            {
                return (T)activity.ChannelData;
            }

            return ((JsonElement)activity.ChannelData).Deserialize<T>(ProtocolJsonSerializer.SerializationOptions);
        }

        /// <summary>
        /// Gets the channel data for this activity as a strongly-typed object.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="T">The type of the object to return.</typeparam>
        /// <param name="instance">When this method returns, contains the strongly-typed object if the operation succeeded,
        /// or the type's default value if the operation failed.</param>
        /// <param name="activity"></param>
        /// <returns>
        /// <c>true</c> if the operation succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <seealso cref="GetChannelData{T}"/>
        public static bool TryGetChannelData<T>(this IActivity activity, out T instance)
        {
            instance = default;

            try
            {
                if (activity.ChannelData == null)
                {
                    return false;
                }

                instance = activity.GetChannelData<T>();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static T CreateReply<T>(this IActivity activity, Func<T> factory) where T : class, IActivity
        {
            var reply = factory();
            reply.Timestamp = DateTime.UtcNow;
            reply.From = new ChannelAccount(id: activity?.Recipient?.Id, name: activity?.Recipient?.Name);
            reply.Recipient = new ChannelAccount(id: activity?.From?.Id, name: activity?.From?.Name);
            reply.ReplyToId = !string.Equals(activity.Type, ActivityTypes.ConversationUpdate, StringComparison.OrdinalIgnoreCase) || activity.ChannelId != "directline" && activity.ChannelId != "webchat" ? activity.Id : null;
            reply.ServiceUrl = activity.ServiceUrl;
            reply.ChannelId = activity.ChannelId;
            reply.Conversation = new ConversationAccount(isGroup: activity.Conversation.IsGroup, id: activity.Conversation.Id, name: activity.Conversation.Name);
            reply.Entities = [];
            return reply;
        }
    }
}