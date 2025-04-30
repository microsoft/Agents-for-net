﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
using Microsoft.Agents.Core.Models;
using System;
using System.Security.Claims;

namespace Microsoft.Agents.Hosting.AspNetCore.BackgroundQueue
{
    /// <summary>
    /// Activity with Claims which should already have been authenticated.
    /// </summary>
    public class ActivityWithClaims
    {
        /// <summary>
        /// Optional: Defaults to IAgent
        /// </summary>
        public Type AgentType { get; set; }

        /// <summary>
        /// <see cref="ClaimsIdentity"/> retrieved from a call to authentication.
        /// </summary>
        public ClaimsIdentity ClaimsIdentity { get; set; }

        /// <summary>
        /// <see cref="Activity"/> to be processed.
        /// </summary>
        public IActivity Activity { get; set; }
        
        public bool IsProactive { get; set; }
        public string ProactiveAudience { get; set; }

        /// <summary>
        /// Invoked when ProcessActivity is done.  Ignored if IsProactive.
        /// </summary>
        public Action<InvokeResponse> OnComplete { get; set; }
    }
}
