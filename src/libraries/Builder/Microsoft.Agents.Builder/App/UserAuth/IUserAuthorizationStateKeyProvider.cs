// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.App.UserAuth
{
    /// <summary>
    /// Provides the storage key used for an OAuth flow's transient authorization state.
    /// </summary>
    public interface IUserAuthorizationStateKeyProvider
    {
        /// <summary>
        /// Gets the storage key for the current turn's transient authorization state.
        /// </summary>
        /// <param name="turnContext">The current turn context.</param>
        /// <returns>The storage key.</returns>
        string GetKey(ITurnContext turnContext);
    }
}
