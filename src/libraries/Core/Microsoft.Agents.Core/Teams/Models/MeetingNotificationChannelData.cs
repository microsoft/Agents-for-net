// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core.Teams.Models
{
    /// <summary>
    /// Specify Teams Bot meeting notification channel data.
    /// </summary>
    public class MeetingNotificationChannelData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MeetingNotificationChannelData"/> class.
        /// </summary>
        public MeetingNotificationChannelData()
        {
        }

        /// <summary>
        /// Gets or sets the OnBehalfOf list for user attribution.
        /// </summary>
        /// <value>The Teams Bot meeting notification's OnBehalfOf list.</value>
#pragma warning disable CA2227 // Collection properties should be read only (we can't change this without breaking binary compat)>
        public IList<OnBehalfOf> OnBehalfOfList { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
