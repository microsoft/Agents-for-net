// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Core.Models;
using System.Security.Claims;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Activity with Claims which should already have been authenticated.
    /// </summary>
    public class ActivityWithClaims
    {
        /// <summary>
        /// <see cref="ClaimsIdentity"/> retrieved from a call to authentication.
        /// </summary>
        public ClaimsIdentity ClaimsIdentity { get; set; }

        /// <summary>
        /// <see cref="Activity"/> to be processed.
        /// </summary>
        public Activity Activity { get; set; }
    }
}
