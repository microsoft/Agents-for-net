
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
        internal static AgentErrorDefinition NullUserTokenProviderIAccessTokenProvider = new AgentErrorDefinition(baseBotBuilderErrorCode - 1, Properties.Resources.IAccessTokenProviderNotFound, "https://aka.ms/AgentsSDK-Error01");

        // ActivityRouteAttribute
        internal static AgentErrorDefinition AttributeSelectorNotFound = new AgentErrorDefinition(baseBotBuilderErrorCode - 2, Properties.Resources.AttributeSelectorNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition AttributeSelectorInvalid = new AgentErrorDefinition(baseBotBuilderErrorCode - 3, Properties.Resources.AttributeSelectorInvalid, "https://aka.ms/AgentsSDK-Error01");

        // UserAuth (base, not Application)
        internal static AgentErrorDefinition UserAuthorizationNotConfigured = new AgentErrorDefinition(baseBotBuilderErrorCode - 4, Properties.Resources.UserAuthorizationNotConfigured, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationRequiresAdapter = new AgentErrorDefinition(baseBotBuilderErrorCode - 5, Properties.Resources.UserAuthorizationRequiresAdapter, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationHandlerNotFound = new AgentErrorDefinition(baseBotBuilderErrorCode - 6, Properties.Resources.UserAuthorizationHandlerNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition FailedToCreateUserAuthorizationHandler = new AgentErrorDefinition(baseBotBuilderErrorCode - 7, Properties.Resources.FailedToCreateUserAuthorizationHandler, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition NoUserAuthorizationHandlers = new AgentErrorDefinition(baseBotBuilderErrorCode - 8, Properties.Resources.NoUserAuthorizationHandlers, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationTypeNotFound = new AgentErrorDefinition(baseBotBuilderErrorCode - 9, Properties.Resources.UserAuthorizationTypeNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationFailed = new AgentErrorDefinition(baseBotBuilderErrorCode - 10, Properties.Resources.UserAuthorizationFailed, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationAlreadyActive = new AgentErrorDefinition(baseBotBuilderErrorCode - 11, Properties.Resources.UserAuthorizationAlreadyActive, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition OBONotExchangeableToken = new AgentErrorDefinition(baseBotBuilderErrorCode - 12, Properties.Resources.OBONotExchangeableToken, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition OBONotSupported = new AgentErrorDefinition(baseBotBuilderErrorCode - 13, Properties.Resources.OBONotSupported, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition OBOExchangeFailed = new AgentErrorDefinition(baseBotBuilderErrorCode - 13, Properties.Resources.OBOExchangeFailed, "https://aka.ms/AgentsSDK-Error01");
    }

}
