// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Linq;
using System;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Core.Models.Activities;

namespace Microsoft.Agents.Core.Models.Entities
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

        public static void NormalizeMentions(this IActivity activity, bool removeMention)
        {
            if (activity is IMessageActivity message)
            {
                if (removeMention)
                {
                    // strip recipient mention tags and text.
                    activity.RemoveRecipientMention();

                    if (activity.Entities != null)
                    {
                        // strip entity.mention records for recipient id.
                        var ListToRemove = activity.Entities.Where(entity => entity is Mention mention &&
                           mention.Mentioned.Id.Equals(activity.Recipient.Id, StringComparison.OrdinalIgnoreCase)).ToList();
                        foreach (var entity in ListToRemove)
                        {
                            activity.Entities.Remove(entity);
                        }
                    }
                }

                // remove <at> </at> tags keeping the inner text.
                message.Text = RemoveAt(message.Text);

                if (activity.Entities != null)
                {
                    // remove <at> </at> tags from mention records keeping the inner text.
                    foreach (var entity in activity.GetMentions())
                    {
                        entity.Text = RemoveAt(entity.Text)?.Trim();
                    }
                }
            }
        }

        private static string RemoveAt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            bool foundTag;
            do
            {
                foundTag = false;
                int iAtStart = text.IndexOf("<at", StringComparison.InvariantCultureIgnoreCase);
                if (iAtStart >= 0)
                {
                    int iAtEnd = text.IndexOf(">", iAtStart, StringComparison.InvariantCultureIgnoreCase);
                    if (iAtEnd > 0)
                    {
                        int iAtClose = text.IndexOf("</at>", iAtEnd, StringComparison.InvariantCultureIgnoreCase);
                        if (iAtClose > 0)
                        {
                            // replace </at> 
                            var followingText = text.Substring(iAtClose + 5);

                            // if first char of followingText is not whitespace
                            if (!char.IsWhiteSpace(followingText.FirstOrDefault()))
                            {
                                // insert space because teams does => <at>Tom</at> is cool => Tom is cool
                                followingText = $" {followingText}";
                            }

#if !NETSTANDARD
                            text = string.Concat(text.AsSpan(0, iAtClose), followingText);

                            // replace <at ...>
                            text = string.Concat(text.AsSpan(0, iAtStart), text.AsSpan(iAtEnd + 1));
#else
                            text = string.Concat(text.Substring(0, iAtClose), followingText);
                            // replace <at ...>
                            text = string.Concat(text.Substring(0, iAtStart), text.Substring(iAtEnd + 1));
#endif

                            // we found one, try again, there may be more.
                            foundTag = true;
                        }
                    }
                }
            }
            while (foundTag);

            return text;
        }

        /// <summary>
        /// Remove recipient mention text from Text property.
        /// Use with caution because this function is altering the text on the Activity.
        /// </summary>
        /// <returns>new .Text property value.</returns>
        public static void RemoveRecipientMention<T>(this T activity) where T : IActivity
        {
            if (activity is IMessageActivity message)
            {
                message.Text = message.RemoveMentionText(activity.Recipient?.Id);
            }
        }

        /// <summary>
        /// Remove any mention text for given id from the Activity.Text property.  For example, given the message
        /// `@echoAgent Hi Agent`, this will remove "@echoAgent", leaving `Hi Agent`.
        /// </summary>
        /// <param name="activity"></param>
        /// <description>
        /// Typically this would be used to remove the mention text for the target recipient (the Agent usually), though
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
            if (activity is IMessageActivity message)
            {
                if (string.IsNullOrEmpty(id)) { return message.Text; }

                foreach (var mention in activity.GetMentions().Where(mention => mention.Mentioned.Id == id))
                {
                    if (mention.Text == null)
                    {
                        message.Text = Regex.Replace(message.Text, "<at>" + Regex.Escape(mention.Mentioned.Name) + "</at>", string.Empty, RegexOptions.IgnoreCase).Trim();
                    }
                    else
                    {
                        message.Text = Regex.Replace(message.Text, Regex.Escape(mention.Text), string.Empty, RegexOptions.IgnoreCase).Trim();
                    }
                }

                return message.Text;
            }

            return null;
        }

        public static string RemoveRecipientMentionText<T>(this T activity) where T : IActivity
        {
            return activity.RemoveMentionText(activity.Recipient?.Id);
        }

        public static bool IsStreamingMessage(this IActivity activity)
        {
            return activity.Type == ActivityTypes.Typing && activity.GetStreamingEntity() != null;
        }

        public static StreamInfo GetStreamingEntity(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }

            return activity.Entities.FirstOrDefault(e => string.Equals(e.Type, EntityTypes.StreamInfo, StringComparison.OrdinalIgnoreCase)) as StreamInfo;
        }

        public static AIEntity GetAIEntity(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }

            return activity.Entities.FirstOrDefault(e => string.Equals(e.Type, EntityTypes.AICitation, StringComparison.OrdinalIgnoreCase)) as AIEntity;
        }

        public static ActivityTreatment GetActivityTreatmentEntity(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }

            return activity.Entities.FirstOrDefault(e => string.Equals(e.Type, EntityTypes.ActivityTreatment, StringComparison.OrdinalIgnoreCase)) as ActivityTreatment;
        }
        
        public static ProductInfo GetProductInfoEntity(this IActivity activity)
        {
            if (activity.Entities == null || activity.Entities.Count == 0)
            {
                return null;
            }

            return activity.Entities.FirstOrDefault(e => string.Equals(e.Type, EntityTypes.ProductInfo, StringComparison.OrdinalIgnoreCase)) as ProductInfo;
        }
    }
}
