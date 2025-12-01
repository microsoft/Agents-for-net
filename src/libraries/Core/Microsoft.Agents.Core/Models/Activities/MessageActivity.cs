using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Core.Models.Activities
{
    public class MessageActivity : Activity, IMessageActivity
    {
        public MessageActivity() : base(ActivityTypes.Message)
        { 
        }

        public MessageActivity(string text, string ssml = null, string inputHint = null, string textFormat = TextFormatTypes.Plain) : base(ActivityTypes.Message)
        {
            SetTextAndSpeak(this, text, ssml, inputHint);
            TextFormat = textFormat;
        }

        public string AttachmentLayout { get; set; }
        public IList<Attachment> Attachments { get; set; } = [];
        public DateTimeOffset? Expiration { get; set; }
        public string Importance { get; set; }
        public string InputHint { get; set; }
        public IList<string> ListenFor { get; set; } = [];
        public SemanticAction SemanticAction { get; set; }
        public string Speak { get; set; }
        public SuggestedActions SuggestedActions { get; set; }
        public string Summary { get; set; }
        public string Text { get; set; }
        public string TextFormat { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }

        private static void SetTextAndSpeak(MessageActivity ma, string text = null, string ssml = null, string inputHint = null)
        {
            // Note: we must put NULL in the fields, as the clients will happily render
            // an empty string, which is not the behavior people expect to see.
            ma.Text = !string.IsNullOrWhiteSpace(text) ? text : null;
            ma.Speak = !string.IsNullOrWhiteSpace(ssml) ? ssml : null;
            ma.InputHint = inputHint ?? InputHints.AcceptingInput;
        }
    }
}
