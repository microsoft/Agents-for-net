
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Client.Errors
{
    /// <summary>
    /// Error helper for the Agent SDK core system
    /// This is used to setup the localized error codes for the AgentSDK
    /// 
    /// Each Error should be created as as an AgentAuthErrorDefinition and added to the ErrorHelper class
    /// Each definition should include an error code as a - from the base error code, a description sorted in the Resource.resx file to support localization, and a help link pointing to an AKA link to get help for the given error. 
    /// 
    /// 
    /// when used, there are is 2 methods in used in the general space. 
    /// Method 1: 
    /// Throw a new exception with the error code, description and helplink
    ///     throw new IndexOutOfRangeException(ErrorHelper.MissingAuthenticationConfiguration.description)
    ///     {
    ///         HResult = ErrorHelper.MissingAuthenticationConfiguration.code,
    ///         HelpLink = ErrorHelper.MissingAuthenticationConfiguration.helplink
    ///     };
    ///
    /// Method 2: 
    /// 
    ///     throw Microsoft.Agents.Core.Errors.ExceptionHelper.GenerateException&lt;OperationCanceledException&gt;(
    ///         ErrorHelper.NullIAccessTokenProvider, ex, $"{AgentClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
    /// 
    /// </summary>
    internal static partial class ErrorHelper
    {
        /// <summary>
        /// Base error code for the authentication provider
        /// </summary>
        private static readonly int baseClientErrorCode = -90000;

        internal static AgentErrorDefinition AgentHostMissingProperty = new AgentErrorDefinition(baseClientErrorCode, Properties.Resources.AgentHostMissingProperty, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition AgentMissingProperty = new AgentErrorDefinition(baseClientErrorCode - 1, Properties.Resources.AgentMissingProperty, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition AgentNotFound = new AgentErrorDefinition(baseClientErrorCode - 2, Properties.Resources.AgentNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition SendToAgentFailed = new AgentErrorDefinition(baseClientErrorCode - 3, Properties.Resources.SendToAgentFailed, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition SendToAgentUnsuccessful = new AgentErrorDefinition(baseClientErrorCode - 4, Properties.Resources.SendToAgentUnsuccessful, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition SendToAgentUnauthorized = new AgentErrorDefinition(baseClientErrorCode - 5, Properties.Resources.SendToAgentUnauthorized, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition AgentTokenProviderNotFound = new AgentErrorDefinition(baseClientErrorCode - 5, Properties.Resources.AgentTokenProviderNotFound, "https://aka.ms/AgentsSDK-Error01");
    }

}
