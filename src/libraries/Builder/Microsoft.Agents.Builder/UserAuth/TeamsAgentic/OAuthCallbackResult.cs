// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.UserAuth.TeamsAgentic
{
    /// <summary>
    /// Transport-agnostic result from the OAuth callback handler.
    /// The hosting layer uses this to render the appropriate HTTP response.
    /// </summary>
    public class OAuthCallbackResult
    {
        /// <summary>
        /// Whether the OAuth code exchange and proactive invoke completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The suggested HTTP status code for the response.
        /// </summary>
        public int StatusCode { get; set; }

        /// <summary>
        /// An error message to display when <see cref="Success"/> is false.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Creates a successful result.
        /// </summary>
        public static OAuthCallbackResult Succeeded()
        {
            return new OAuthCallbackResult { Success = true, StatusCode = 200 };
        }

        /// <summary>
        /// Creates a failed result with the specified status code and message.
        /// </summary>
        public static OAuthCallbackResult Failed(int statusCode, string errorMessage)
        {
            return new OAuthCallbackResult { Success = false, StatusCode = statusCode, ErrorMessage = errorMessage };
        }
    }
}
