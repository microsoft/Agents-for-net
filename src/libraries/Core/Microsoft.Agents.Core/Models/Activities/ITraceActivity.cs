// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Represents a point in a Agent's logic, to help with Agent debugging.
    /// </summary>
    /// <remarks>
    /// The trace activity typically is logged by transcript history components to become part of a
    /// transcript history. In remote debugging scenarios the trace activity can be sent to the client
    /// so that the activity can be inspected as part of the debug flow.
    ///
    /// Trace activities are normally not shown to the user, and are internal to transcript logging
    /// and developer debugging.
    ///
    /// See also InspectionMiddleware.
    /// </remarks>
    public interface ITraceActivity : IActivity
    {
        /// <summary>
        /// The label field contains optional a label which can provide contextual information about the trace. The value of 
        /// the label field is of type string.
        /// </summary>
        string Label { get; set; }

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
