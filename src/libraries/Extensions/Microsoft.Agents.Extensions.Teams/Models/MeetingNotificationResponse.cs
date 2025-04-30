// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Extensions.Teams.Models
{
    /// <summary>
    /// Specifies Bot meeting notification response.
    /// Contains list of <see cref="MeetingNotificationRecipientFailureInfo"/>.
    /// </summary>
    public class MeetingNotificationResponse
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeetingNotificationResponse"/> class.
        /// </summary>
        public MeetingNotificationResponse()
        {
        }

        /// <summary>
        /// Gets or sets the list of <see cref="MeetingNotificationRecipientFailureInfo"/>.
        /// </summary>
        /// <value>The list of <see cref="MeetingNotificationRecipientFailureInfo"/>.</value>
        public IList<MeetingNotificationRecipientFailureInfo> RecipientsFailureInfo { get; set; }
    }
}
