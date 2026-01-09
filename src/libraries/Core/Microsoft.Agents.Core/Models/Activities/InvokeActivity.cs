// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
    public class InvokeActivity : Activity, IInvokeActivity
    {
        public InvokeActivity() : base(ActivityTypes.Invoke)
        {

        }

        public InvokeActivity(string name) : base(ActivityTypes.Invoke)
        {
            Name = name;
        }

        public string Name { get; set; }
        //!!!public ConversationReference RelatesTo { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
