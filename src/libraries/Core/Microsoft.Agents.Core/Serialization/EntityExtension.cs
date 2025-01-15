// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Core.Serialization
{
    public static class EntityExtension
    {
        /// <summary>
        /// Retrieve internal payload.
        /// </summary>
        /// <typeparam name="T">T.</typeparam>
        /// <returns>T as T.</returns>
        public static T GetAs<T>(this Entity entity)
        {
            return ProtocolJsonSerializer.GetAs<T, Entity>(entity);
        }

        /// <summary>
        /// Set internal payload.
        /// </summary>
        /// <typeparam name="T">T.</typeparam>
        /// <param name="entity"></param>
        /// <param name="obj">obj.</param>
        public static void SetAs<T>(this Entity entity, T obj)
        {
            var copy = ProtocolJsonSerializer.CloneTo<Entity>(obj);
            entity.Type = copy.Type;
            entity.Properties = copy.Properties;
        }
        

        /// <summary>
        /// Remove any mention text for given id from the Activity.Text property.  For example, given the message
        /// @echoBot Hi Bot, this will remove "@echoBot", leaving "Hi Bot".
        /// </summary>
        /// <param name="activity"></param>
        /// <description>
        /// Typically this would be used to remove the mention text for the target recipient (the bot usually), though
        /// it could be called for each member.  For example:
        ///    turnContext.Activity.RemoveMentionText(turnContext.Activity.Recipient.Id);
        /// The format of a mention Activity.Entity is dependent on the Channel.  But in all cases we
        /// expect the Mention.Text to contain the exact text for the user as it appears in
        /// Activity.Text.
        /// For example, Teams uses &lt;at&gt;username&lt;/at&gt;, whereas slack use @username. It
        /// is expected that text is in Activity.Text and this method will remove that value from
        /// Activity.Text.
        /// </description>
        /// <param name="id">id to match.</param>
        /// <returns>new Activity.Text property value.</returns>
        public static string RemoveMentionText(this IActivity activity, string id)
        {
            foreach (var mention in activity.GetMentions().Where(mention => mention.Mentioned.Id == id))
            {
                if (mention.Text == null)
                {
                    activity.Text = Regex.Replace(activity.Text, "<at>" + Regex.Escape(mention.Mentioned.Name) + "</at>", string.Empty, RegexOptions.IgnoreCase).Trim();
                }
                else
                {
                    activity.Text = Regex.Replace(activity.Text, Regex.Escape(mention.Text), string.Empty, RegexOptions.IgnoreCase).Trim();
                }
            }

            return activity.Text;
        }
    }
}
