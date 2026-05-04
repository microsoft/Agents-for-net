// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Core.Models
{
    [ActivityType(ActivityTypes.CommandResult)]
    public class CommandResultActivity : Activity, ICommandResultActivity
    {
        public CommandResultActivity() : base(ActivityTypes.CommandResult)
        {
        }

        public string Name { get; set; }

        [JsonConverter(typeof(Serialization.Converters.ObjectTypeConverter))]
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
