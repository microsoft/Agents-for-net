// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Synchronous request to invoke an operation.
    /// </summary>
    public interface IInvokeActivity : IActivity
    {
        /// <summary>
        /// The name field controls the meaning of the event and the schema of the value field.
        /// </summary>
        string Name { get; set; }

        /*!!!
        /// <summary>
        /// The relatesTo field references another conversation, and optionally a specific Activity within that conversation. 
        /// The value of the relatesTo field is a complex object of the Conversation reference type.
        /// </summary>
        ConversationReference RelatesTo { get; set; }
        */

        /// <summary>
        /// The value field contains a programmatic payload specific to the Activity being sent. Its meaning and format 
        /// are defined in other sections of this document that describe its use.
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// The valueType field is a string type which contains a unique value which identifies the shape of the value.
        /// </summary>
        string ValueType { get; set; }
    }
}
