// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Core.Interfaces
{
    public class TurnStateKeys
    {
        /// <summary>
        /// The key value for any InvokeResponseActivity that would be on the TurnState.
        /// </summary>
        public const string InvokeResponseKey = "TurnState.InvokeResponse";

        /// <summary>
        /// The string value for the bot identity key.
        /// </summary>
        public const string BotIdentityKey = "TurnState.BotIdentity";

        /// <summary>
        /// The string value for the OAuth scope key.
        /// </summary>
        public const string OAuthScopeKey = "TurnState.OAuthScope";

    }
}
