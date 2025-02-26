// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.BotBuilder.UserAuth
{
    /// <summary>
    /// Cause of user authentication exception.
    /// </summary>
    public enum AuthExceptionReason
    {
        /// <summary>
        /// The authentication flow completed without a token.
        /// </summary>
        CompletionWithoutToken,

        /// <summary>
        /// The incoming activity is not valid for sign in flow.
        /// </summary>
        InvalidActivity,

        /// <summary>
        /// The the flow timed out (C2 didn't respond in time).
        /// </summary>
        Timeout,

        InvalidSignIn,

        /// <summary>
        /// Other error.
        /// </summary>
        Other
    }

    /// <summary>
    /// An exception thrown when user authentication error occurs.
    /// </summary>
    public class AuthException : Exception
    {
        /// <summary>
        /// The cause of the exception.
        /// </summary>
        public AuthExceptionReason Cause { get; }

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="message">The exception message</param>
        /// <param name="reason">The cause of the exception</param>
        public AuthException(string message, AuthExceptionReason reason = AuthExceptionReason.Other) : base(message)
        {
            Cause = reason;
        }
    }
}
