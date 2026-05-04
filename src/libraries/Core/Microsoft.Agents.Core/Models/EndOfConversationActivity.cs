// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    [ActivityType(ActivityTypes.EndOfConversation)]
    public class EndOfConversationActivity : Activity, IEndOfConversationActivity
    {
        public EndOfConversationActivity() : base(ActivityTypes.EndOfConversation)
        {

        }

        public string Code { get; set; }
        public string Text { get; set; }
        public string TextFormat { get; set; }

        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
