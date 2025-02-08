
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.BotBuilder.Errors
{
    /// <summary>
    /// Error helper for the Authentication core system
    /// This is used to setup the localized error codes for the authentication subsystem of the AgentSDK
    /// 
    /// Note: specific auth providers are expected to implement their own error codes inside their own libraries. 
    /// </summary>
    internal static partial class ErrorHelper
    {
        /// <summary>
        /// Base error code for the authentication provider
        /// </summary>
        private static readonly int baseBotBuilderErrorCode = -50000;

        internal static AgentErrorDefinition NullIAccessTokenProvider = new AgentErrorDefinition(baseBotBuilderErrorCode, Properties.Resources.IAccessTokenProviderNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition NullUserTokenProviderIAccessTokenProvider = new AgentErrorDefinition(baseBotBuilderErrorCode-1, Properties.Resources.IAccessTokenProviderNotFound, "https://aka.ms/AgentsSDK-Error01");

    }

}
