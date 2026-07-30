// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Public Extensions for <see cref="Microsoft.Agents.Core.Models.IActivity"/>. type
    /// </summary>
    public static class IActivityExtensions
    {
        /// <summary>
        /// Converts an <see cref="Microsoft.Agents.Core.Models.IActivity"/> to a JSON string.
        /// </summary>
        /// <param name="activity">Activity to convert to Json Payload</param>
        /// <returns>JSON String</returns>
        public static string ToJson(this IActivity activity)
        {
            return JsonSerializer.Serialize(activity, ProtocolJsonSerializer.SerializationOptions);
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
        /// <seealso cref="Microsoft.Agents.Core.Models.IActivityExtensions.GetChannelData{T}(Microsoft.Agents.Core.Models.IActivity)"/>
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
    }
}