// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
    public class EndOfConversationActivity : Activity, IEndOfConversationActivity
    {
        public EndOfConversationActivity() : base(ActivityTypes.EndOfConversation)
        {

        }

        public string Code { get; set; }
        public string Text { get; set; }
        public string TextFormat { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
