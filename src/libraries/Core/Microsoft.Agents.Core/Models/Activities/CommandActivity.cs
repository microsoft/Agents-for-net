// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
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
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
