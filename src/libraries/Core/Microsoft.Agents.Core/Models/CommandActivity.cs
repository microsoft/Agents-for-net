// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    [ActivityType(ActivityTypes.Command)]
    public class CommandActivity : Activity, ICommandActivity
    {
        public CommandActivity() : base(ActivityTypes.Command)
        { 
        }

        public CommandActivity(string name) : base(ActivityTypes.Command)
        {
            Name = name;
        }

        public string Name { get; set; }

        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
