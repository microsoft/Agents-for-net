// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System;
using System.Text.Json;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Core.Models
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
            return JsonSerializer.Serialize(activity, ProtocolJsonSerializer.SerializationOptions);
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
            return result.ToArray();
        }

        /// <summary>
        /// Remove recipient mention text from Text property.
        /// Use with caution because this function is altering the text on the Activity.
        /// </summary>
        /// <returns>new .Text property value.</returns>
        public static string RemoveRecipientMention<T>(this T activity) where T : IActivity
        {
            return activity.RemoveMentionText(activity.Recipient?.Id);
        }

        /// <summary>
        /// Clone the activity to a new instance of activity. 
        /// </summary>
        /// <param name="activity"></param>
        /// <returns></returns>
        public static IActivity Clone(this IActivity activity)
        {
            var json = JsonSerializer.Serialize(activity, ProtocolJsonSerializer.SerializationOptions);
            return JsonSerializer.Deserialize<Activity>(json, ProtocolJsonSerializer.SerializationOptions);
        }
        /// <summary>
        /// Gets the channel data for this activity as a strongly-typed object.
        /// </summary>
        /// <typeparam name="TypeT">The type of the object to return.</typeparam>
        /// <returns>The strongly-typed object; or the type's default value, if the ChannelData is null.</returns>
#pragma warning disable CA1715 // Identifiers should have correct prefix (we can't change it without breaking binary compatibility)
        public static TypeT GetChannelData<TypeT>(this IActivity activity)
#pragma warning restore CA1715 // Identifiers should have correct prefix
        {
            if (activity.ChannelData == null)
            {
                return default;
            }

            if (activity.ChannelData.GetType() == typeof(TypeT))
            {
                return (TypeT)activity.ChannelData;
            }

            return ((JsonElement)activity.ChannelData).Deserialize<TypeT>(ProtocolJsonSerializer.SerializationOptions);


        }

        /// <summary>
        /// Gets the channel data for this activity as a strongly-typed object.
        /// A return value indicates whether the operation succeeded.
        /// </summary>
        /// <typeparam name="TypeT">The type of the object to return.</typeparam>
        /// <param name="instance">When this method returns, contains the strongly-typed object if the operation succeeded,
        /// or the type's default value if the operation failed.</param>
        /// <param name="activity"></param>
        /// <returns>
        /// <c>true</c> if the operation succeeded; otherwise, <c>false</c>.
        /// </returns>
        /// <seealso cref="GetChannelData{TypeT}"/>
#pragma warning disable CA1715 // Identifiers should have correct prefix (we can't change it without breaking binary compatibility)
        public static bool TryGetChannelData<TypeT>(this IActivity activity, out TypeT instance)
#pragma warning restore CA1715 // Identifiers should have correct prefix
        {
            instance = default;


            try
            {
                if (activity.ChannelData == null)
                {
                    return false;
                }

                instance = activity.GetChannelData<TypeT>();
                return true;
            }
#pragma warning disable CA1031 // Do not catch general exception types (we just return false here if the conversion fails for any reason)
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
                return false;
            }
        }
    }
}