// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.UserAuth.TeamsAgentic
{
    /// <summary>
    /// Transport-agnostic input for the OAuth callback handler.
    /// The hosting layer extracts these values from the HTTP request (or other transport)
    /// and passes them to <see cref="TeamsAgenticCallbackHandler"/>.
    /// </summary>
    public class OAuthCallbackInput
    {
        /// <summary>
        /// The authorization code returned by Azure AD.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// The state parameter (nonce) used to correlate the callback with the pending OAuth flow.
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// The error code returned by Azure AD, if the authorization failed.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// A human-readable description of the error, if the authorization failed.
        /// </summary>
        public string ErrorDescription { get; set; }
    }
}
