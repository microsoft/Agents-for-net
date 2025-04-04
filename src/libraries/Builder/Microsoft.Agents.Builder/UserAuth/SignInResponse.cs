﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.UserAuth
{
    /// <summary>
    /// The sign-in response
    /// </summary>
    /// <remarks>
    /// Initialize an instance of current class
    /// </remarks>
    /// <param name="status">The sign in status</param>
    public class SignInResponse(SignInStatus status)
    {
        /// <summary>
        /// The sign-in status
        /// </summary>
        public SignInStatus Status { get; set; } = status;

        /// <summary>
        /// The non-success message. Only available when sign-in status is Error.
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// The cause of error. Only available when sign-in status is Error.
        /// </summary>
        public AuthExceptionReason? Cause { get; set; }

        /// <summary>
        /// The token response.  Only available when sign-in status is Complete.
        /// </summary>
        public string Token { get; set; }
    }
}
