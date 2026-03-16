// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace Microsoft.Agents.Core.Models
{
    public class InvokeResponseActivity : Activity, IInvokeResponseActivity
    {
        public InvokeResponseActivity() : base(ActivityTypes.InvokeResponse)
        {
            Value = new InvokeResponse { Status = (int)HttpStatusCode.OK };
        }

        public InvokeResponseActivity(object body, int status = (int)HttpStatusCode.OK) : base(ActivityTypes.InvokeResponse)
        {
            Value = new InvokeResponse { Status = status, Body = body };
        }

        public InvokeResponseActivity(InvokeResponse response) : base(ActivityTypes.InvokeResponse)
        {
            Value = response;
        }

        //!!!public ConversationReference RelatesTo { get; set; }
        public object Value { get; set; }
        public string ValueType { get; set; }
    }
}
