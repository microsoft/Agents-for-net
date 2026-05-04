// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    public class EventActivity : Activity, IEventActivity
    {
        public EventActivity(string name) : base(ActivityTypes.Event)
        { 
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));
            Name = name;
        }

        public string Name { get; set; }
        //!!!public ConversationReference RelatesTo { get; set; }

        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
