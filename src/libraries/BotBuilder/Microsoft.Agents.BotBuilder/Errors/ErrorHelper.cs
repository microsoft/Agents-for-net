
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.BotBuilder.Errors
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
    ///         ErrorHelper.NullIAccessTokenProvider, ex, $"{BotClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
    /// 
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
