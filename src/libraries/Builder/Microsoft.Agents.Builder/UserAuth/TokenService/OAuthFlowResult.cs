// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;

namespace Microsoft.Agents.Builder.UserAuth.TokenService
{
    /// <summary>
    /// Represents the outcome status of an OAuth flow continuation.
    /// </summary>
    public enum OAuthFlowStatus
    {
        /// <summary>
        /// A token was successfully obtained. <see cref="OAuthFlowResult.TokenResponse"/> is non-null.
        /// </summary>
        Complete,

        /// <summary>
        /// The flow is still in progress; no token yet.
        /// </summary>
        Pending,

        /// <summary>
        /// The flow expired before a token was obtained.
        /// </summary>
        TimedOut,

        /// <summary>
        /// The user explicitly cancelled the sign-in.
        /// </summary>
        UserCancelled,

        /// <summary>
        /// The sign-in flow completed with an error response.
        /// </summary>
        SignInFailed,
    }

    /// <summary>
    /// The structured result returned by <see cref="OAuthFlow.ContinueFlowAsync"/>.
    /// </summary>
    public sealed class OAuthFlowResult
    {
        private static readonly OAuthFlowResult _pending = new(OAuthFlowStatus.Pending);
        private static readonly OAuthFlowResult _timedOut = new(OAuthFlowStatus.TimedOut);
        private static readonly OAuthFlowResult _userCancelled = new(OAuthFlowStatus.UserCancelled);

        private OAuthFlowResult(OAuthFlowStatus status, TokenResponse tokenResponse = null, ErrorResponse errorResponse = null)
        {
            Status = status;
            TokenResponse = tokenResponse;
            ErrorResponse = errorResponse;
        }

        /// <summary>
        /// The outcome of the flow continuation.
        /// </summary>
        public OAuthFlowStatus Status { get; }

        /// <summary>
        /// The token response. Non-null only when <see cref="Status"/> is <see cref="OAuthFlowStatus.Complete"/>.
        /// </summary>
        public TokenResponse TokenResponse { get; }

        /// <summary>
        /// The sign-in failure payload. Non-null only when <see cref="Status"/> is <see cref="OAuthFlowStatus.SignInFailed"/>.
        /// </summary>
        public ErrorResponse ErrorResponse { get; }

        /// <summary>Singleton result for a still-pending flow.</summary>
        public static OAuthFlowResult Pending => _pending;

        /// <summary>Singleton result for a timed-out flow.</summary>
        public static OAuthFlowResult TimedOut => _timedOut;

        /// <summary>Singleton result for a user-cancelled flow.</summary>
        public static OAuthFlowResult UserCancelled => _userCancelled;

        /// <summary>Creates a completed result carrying the obtained token.</summary>
        public static OAuthFlowResult Complete(TokenResponse tokenResponse)
            => new(OAuthFlowStatus.Complete, tokenResponse);

        /// <summary>Creates a failed result carrying the sign-in failure payload.</summary>
        public static OAuthFlowResult SignInFailed(ErrorResponse errorResponse)
            => new(OAuthFlowStatus.SignInFailed, errorResponse: errorResponse);
    }
}
