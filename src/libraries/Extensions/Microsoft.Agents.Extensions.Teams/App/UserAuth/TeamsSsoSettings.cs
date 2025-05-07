// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    /// <summary>
    /// Settings to initialize TeamsSsoAuthentication class
    /// </summary>
    public class TeamsSsoSettings
    {
        /// <summary>
        /// The AAD scopes for authentication. Only one resource is allowed in the scopes.
        /// </summary>
        public string[] Scopes { get; set; } = [];

        /// <summary>
        /// Name of the IConnections token provider to use.
        /// </summary>
        public string ConnectionName { get; set; }

        /// <summary>
        /// The sign in link for authentication.
        /// The library will pass `scope`, `clientId`, and `tenantId` to the link as query parameters.
        /// Your sign in page can leverage these parameters to compose the AAD sign-in URL.
        /// </summary>
        public string SignInLink { get; set; }

        /// <summary>
        /// Number of milliseconds to wait for the user to authenticate.
        /// Defaults to a value `900,000` (15 minutes).
        /// Only works in conversional bot scenario.
        /// </summary>
        public int? Timeout { get; set; }

        /// <summary>
        /// Value indicating whether the authentication should end upon receiving an invalid message.
        /// Defaults to `true`.
        /// Only works in conversional bot scenario.
        /// </summary>
        public bool EndOnInvalidMessage { get; set; }

        public string InvalidSignInRetryMessage { get; set; } = "Invalid sign in. Please try again.";
        public int InvalidSignInRetryMax { get; set; } = 2;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="connectionName"></param>
        /// <param name="scopes">The AAD scopes for authentication.</param>
        /// <param name="signInLink">The sign in link for authentication.</param>
        /// <param name="timeout">Number of milliseconds to wait for the user to authenticate.</param>
        /// <param name="endOnInvalidMessage">Value indicating whether the authentication should end upon receiving an invalid message.</param>
        public TeamsSsoSettings(string connectionName, string[] scopes, string signInLink, int timeout = 900000, bool endOnInvalidMessage = true)
        {
            Scopes = scopes;
            ConnectionName = connectionName;
            SignInLink = signInLink;
            Timeout = timeout;
            EndOnInvalidMessage = endOnInvalidMessage;
        }
    }
}
