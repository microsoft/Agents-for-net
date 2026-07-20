// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

namespace Microsoft.Agents.Core.Models
{
    /// <summary>
    /// Fluent, chainable builder members for <see cref="Microsoft.Agents.Core.Models.Activity"/>.
    /// </summary>
    /// <remarks>
    /// These helpers make it easy to construct and enrich activities in a single expression, for
    /// example: <c>Activity.CreateMessageActivity().WithText("hi").AddAttachment(card).AddMention(user)</c>.
    /// Every mutating method returns the same activity instance so calls can be chained.
    /// </remarks>
    public partial class Activity
    {
        /// <inheritdoc/>
        public IActivity WithText(string text)
        {
            Text = text;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithSpeak(string speak)
        {
            Speak = speak;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithInputHint(string inputHint)
        {
            InputHint = inputHint;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithSummary(string summary)
        {
            Summary = summary;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithLocale(string locale)
        {
            Locale = locale;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithTextFormat(string textFormat)
        {
            TextFormat = textFormat;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithAttachmentLayout(string attachmentLayout)
        {
            AttachmentLayout = attachmentLayout;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithDeliveryMode(string deliveryMode)
        {
            DeliveryMode = deliveryMode;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithName(string name)
        {
            Name = name;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithValue(object value)
        {
            Value = value;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithValue(object value, string valueType)
        {
            Value = value;
            ValueType = valueType;
            return this;
        }

        /// <inheritdoc/>
        public IActivity WithSuggestedActions(SuggestedActions suggestedActions)
        {
            SuggestedActions = suggestedActions;
            return this;
        }

        /// <inheritdoc/>
        public IActivity AddText(string text)
        {
            Text += text;
            return this;
        }

        /// <inheritdoc/>
        public IActivity AddAttachment(params Attachment[] attachments)
        {
            if (attachments == null)
            {
                return this;
            }

            Attachments ??= [];
            foreach (var attachment in attachments)
            {
                Attachments.Add(attachment);
            }

            return this;
        }

        /// <inheritdoc/>
        public IActivity AddCard(Card card) => card == null ? this : AddAttachment(card.ToAttachment());

        /// <inheritdoc/>
        public IActivity AddEntity(params Entity[] entities)
        {
            if (entities == null)
            {
                return this;
            }

            Entities ??= [];
            foreach (var entity in entities)
            {
                Entities.Add(entity);
            }

            return this;
        }

        /// <inheritdoc/>
        public IActivity AddMention(ChannelAccount account, string text = null, bool addText = true)
        {
            var mentionText = text ?? account?.Name;
            var markup = $"<at>{mentionText}</at>";

            if (addText)
            {
                Text = string.IsNullOrEmpty(Text) ? markup : $"{markup} {Text}";
            }

            return AddEntity(new Mention(mentioned: account, text: markup));
        }

        /// <inheritdoc/>
        public Mention GetAccountMention(string accountId)
        {
            if (Entities == null || accountId == null)
            {
                return null;
            }

            foreach (var entity in Entities)
            {
                if (entity is Mention mention && mention.Mentioned?.Id == accountId)
                {
                    return mention;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public bool IsRecipientMentioned()
        {
            return Recipient?.Id != null && GetAccountMention(Recipient.Id) != null;
        }

        /// <inheritdoc/>
        public bool IsMessage() => IsType(ActivityTypes.Message);

        /// <inheritdoc/>
        public bool IsEvent() => IsType(ActivityTypes.Event);

        /// <inheritdoc/>
        public bool IsInvoke() => IsType(ActivityTypes.Invoke);

        /// <inheritdoc/>
        public bool IsTyping() => IsType(ActivityTypes.Typing);

        /// <inheritdoc/>
        public bool IsConversationUpdate() => IsType(ActivityTypes.ConversationUpdate);

        /// <inheritdoc/>
        public bool IsEndOfConversation() => IsType(ActivityTypes.EndOfConversation);

        /// <inheritdoc/>
        public bool IsHandoff() => IsType(ActivityTypes.Handoff);

        /// <inheritdoc/>
        public bool IsTrace() => IsType(ActivityTypes.Trace);

        /// <inheritdoc/>
        public bool IsCommand() => IsType(ActivityTypes.Command);

        /// <inheritdoc/>
        public bool IsCommandResult() => IsType(ActivityTypes.CommandResult);
    }
}
