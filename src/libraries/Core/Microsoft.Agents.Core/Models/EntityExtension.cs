// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using System.Text;
using System.Linq;
using System;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Core.Models
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
            if (activity.Type == ActivityTypes.Message)
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
                activity.Text = RemoveAt(activity.Text);

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

            const string openingTag = "<at";
            const string closingTag = "</at";
            StringBuilder normalized = null;
            int position = 0;
            int copyStart = 0;
            int openingStart = -1;
            int contentStart = 0;

            while (position < text.Length)
            {
                int tagStart = text.IndexOf('<', position);
                if (tagStart < 0)
                {
                    break;
                }

                bool isOpening = text.Length - tagStart >= openingTag.Length
                    && string.Compare(text, tagStart, openingTag, 0, openingTag.Length, StringComparison.OrdinalIgnoreCase) == 0;
                bool isClosing = text.Length - tagStart >= closingTag.Length
                    && string.Compare(text, tagStart, closingTag, 0, closingTag.Length, StringComparison.OrdinalIgnoreCase) == 0;
                int tagNameLength = isOpening ? openingTag.Length : isClosing ? closingTag.Length : 0;
                int tagNameEnd = tagStart + tagNameLength;
                if (tagNameLength == 0
                    || (tagNameEnd < text.Length && text[tagNameEnd] != '>' && !char.IsWhiteSpace(text[tagNameEnd])))
                {
                    position = tagStart + 1;
                    continue;
                }

                // Invalid structure leaves the whole field unchanged, including earlier valid pairs.
                if (isOpening)
                {
                    if (openingStart >= 0)
                    {
                        return text;
                    }

                    int tagEnd = text.IndexOf('>', tagNameEnd);
                    if (tagEnd < 0)
                    {
                        return text;
                    }

                    openingStart = tagStart;
                    contentStart = tagEnd + 1;
                    position = contentStart;
                    continue;
                }

                if (openingStart < 0 || tagNameEnd == text.Length || text[tagNameEnd] != '>')
                {
                    return text;
                }

                normalized ??= new StringBuilder(text.Length);
                normalized.Append(text, copyStart, openingStart - copyStart);
                normalized.Append(text, contentStart, tagStart - contentStart);
                position = tagNameEnd + 1;
                if (position == text.Length || !char.IsWhiteSpace(text[position]))
                {
                    normalized.Append(' ');
                }

                copyStart = position;
                openingStart = -1;
            }

            if (openingStart >= 0 || normalized == null)
            {
                return text;
            }

            return normalized.Append(text, copyStart, text.Length - copyStart).ToString();
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
            if (string.IsNullOrEmpty(id)) { return activity.Text; }

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
