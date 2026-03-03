// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Extensions.Teams.App.UserAuth
{
    /// <summary>
    /// Settings to initialize TeamsSsoAuthentication class
    /// </summary>
    public class TeamsSsoSettings //: OAuthSettings
    {
        public static readonly TimeSpan DefaultTimeoutValue = TimeSpan.FromMinutes(15);

        /// <summary>
        /// The AAD scopes for authentication. Only one resource is allowed in the scopes.
        /// </summary>
        public string[] Scopes { get; set; } = [];

        /// <summary>
        /// Name of the IConnections token provider to use.  Typically this is the same Connection 
        /// the Agent uses for incoming/outgoing Teams requests.
        /// </summary>
        public string SsoConnectionName { get; set; }

        /// <summary>
        /// The sign in link for authentication.
        /// The library will pass `scope`, `clientId`, and `tenantId` to the link as query parameters.
        /// Your sign in page can leverage these parameters to compose the AAD sign-in URL.
        /// </summary>
        public string SignInLink { get; set; }

        public int? Timeout { get; set; } = (int)DefaultTimeoutValue.TotalMilliseconds;

        public bool EndOnInvalidMessage { get; set; }

        public string InvalidSignInRetryMessage { get; set; } = "Invalid sign in code. Please enter the 6-digit code.";
        public int InvalidSignInRetryMax { get; set; } = 2;

        /// <summary>
        /// Initializes the class
        /// </summary>
        /// <param name="ssoConnectionName"></param>
        /// <param name="scopes">The AAD scopes for authentication.</param>
        /// <param name="signInLink">The sign in link for authentication.</param>
        /// <param name="timeout">Number of milliseconds to wait for the user to authenticate.</param>
        /// <param name="endOnInvalidMessage">Value indicating whether the authentication should end upon receiving an invalid message.</param>
        public TeamsSsoSettings(string ssoConnectionName, string[] scopes, string signInLink, int timeout = 900000, bool endOnInvalidMessage = true)
        {
            Scopes = scopes;
            SsoConnectionName = ssoConnectionName;
            SignInLink = signInLink;
            Timeout = timeout;
            EndOnInvalidMessage = endOnInvalidMessage;
        }
    }
}
