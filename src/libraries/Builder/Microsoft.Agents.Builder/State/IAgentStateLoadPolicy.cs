// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Builder.State
{
    /// <summary>
    /// Determines whether an agent state scope is available to load for a turn.
    /// </summary>
    public interface IAgentStateLoadPolicy
    {
        /// <summary>
        /// Determines whether the specified state scope should be loaded.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="agentState">The state scope being loaded.</param>
        /// <returns><c>true</c> to load the state scope; otherwise, <c>false</c>.</returns>
        bool ShouldLoad(ITurnContext turnContext, IAgentState agentState);
    }
}
