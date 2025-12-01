// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. 

namespace Microsoft.Agents.Core.Models.Activities
{
    /// <summary>
    /// Defines values for handoff event names.
    /// </summary>
    internal static class HandoffEventNames
    {
        /// <summary>
        /// The value of handoff events for initiate handoff.
        /// </summary>
        public const string InitiateHandoff = "handoff.initiate";

        /// <summary>
        /// The value of handoff events for handoff status.
        /// </summary>
        public const string HandoffStatus = "handoff.status";
    }
}
