// Licensed under the MIT License.
// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.BotBuilder.Dialogs
{
    /// <summary>
    /// Extension method on object <see cref="object"/>.
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Extension Method on object to cast to type T to support TypeNameHandling.None during storage serialization.
        /// </summary>
        /// <param name="obj">object to cast.</param>
        /// <typeparam name="T">type to which object should be casted.</typeparam>
        /// <returns>T.</returns>
        public static T CastTo<T>(this object obj)
        {
            return ProtocolJsonSerializer.ToObject<T>(obj);
        }
    }
}
