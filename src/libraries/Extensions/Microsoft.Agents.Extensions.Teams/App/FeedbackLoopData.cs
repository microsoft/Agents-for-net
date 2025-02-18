// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.App
{
    /// <summary>
    /// Data returned when the thumbsup or thumbsdown button is clicked and response is received.
    /// </summary>
    public class FeedbackLoopData
    {
        /// <summary>
        /// The action name.
        /// </summary>
        public string ActionName { get; set; } = "feedback";

        /// <summary>
        /// The action value.
        /// </summary>
        public FeedbackLoopDataActionValue? ActionValue { get; set; }

        /// <summary>
        /// The activity ID that the feedback provided on.
        /// </summary>
        public string? ReplyToId { get; set; }
    }

    /// <summary>
    /// The feedback loop data's action value.
    /// </summary>
    public class FeedbackLoopDataActionValue
    {
        /// <summary>
        /// Either "like" or "dislike"
        /// </summary>
        public string? Reaction { get; set; }

        /// <summary>
        /// The feedback provided by the user when prompted with "What did you lke/dislike?"
        /// </summary>
        public string? Feedback { get; set; }
    }
}
