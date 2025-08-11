
using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Builder.Errors
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
        private static readonly int baseBuilderErrorCode = -50000;

        internal static AgentErrorDefinition NullIAccessTokenProvider = new AgentErrorDefinition(baseBuilderErrorCode, Properties.Resources.IAccessTokenProviderNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition NullUserTokenProviderIAccessTokenProvider = new AgentErrorDefinition(baseBuilderErrorCode - 1, Properties.Resources.IAccessTokenProviderNotFound, "https://aka.ms/AgentsSDK-Error01");

        // Application RouteAttribute
        internal static AgentErrorDefinition AttributeSelectorNotFound = new AgentErrorDefinition(baseBuilderErrorCode - 2, Properties.Resources.AttributeSelectorNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition AttributeSelectorInvalid = new AgentErrorDefinition(baseBuilderErrorCode - 3, Properties.Resources.AttributeSelectorInvalid, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition AttributeHandlerInvalid = new AgentErrorDefinition(baseBuilderErrorCode - 4, Properties.Resources.AttributeHandlerInvalid, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition AttributeMissingArgs = new AgentErrorDefinition(baseBuilderErrorCode - 5, Properties.Resources.AttributeMissingArgs, "https://aka.ms/AgentsSDK-Error01");

        // StreamingMessages
        internal static AgentErrorDefinition StreamingResponseEnded = new AgentErrorDefinition(baseBuilderErrorCode - 6, Properties.Resources.StreamingResponseEnded, "https://aka.ms/AgentsSDK-Error01");

        // UserAuth (base, not Application)
        internal static AgentErrorDefinition UserAuthorizationNotConfigured = new AgentErrorDefinition(baseBuilderErrorCode - 8, Properties.Resources.UserAuthorizationNotConfigured, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationRequiresAdapter = new AgentErrorDefinition(baseBuilderErrorCode - 9, Properties.Resources.UserAuthorizationRequiresAdapter, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationHandlerNotFound = new AgentErrorDefinition(baseBuilderErrorCode - 10, Properties.Resources.UserAuthorizationHandlerNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationDefaultHandlerNotFound = new AgentErrorDefinition(baseBuilderErrorCode - 10, Properties.Resources.UserAuthorizationDefaultHandlerNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition FailedToCreateUserAuthorizationHandler = new AgentErrorDefinition(baseBuilderErrorCode - 11, Properties.Resources.FailedToCreateUserAuthorizationHandler, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition NoUserAuthorizationHandlers = new AgentErrorDefinition(baseBuilderErrorCode - 12, Properties.Resources.NoUserAuthorizationHandlers, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationTypeNotFound = new AgentErrorDefinition(baseBuilderErrorCode - 13, Properties.Resources.UserAuthorizationTypeNotFound, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationFailed = new AgentErrorDefinition(baseBuilderErrorCode - 14, Properties.Resources.UserAuthorizationFailed, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserAuthorizationAlreadyActive = new AgentErrorDefinition(baseBuilderErrorCode - 15, Properties.Resources.UserAuthorizationAlreadyActive, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition OBONotExchangeableToken = new AgentErrorDefinition(baseBuilderErrorCode - 16, Properties.Resources.OBONotExchangeableToken, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition OBONotSupported = new AgentErrorDefinition(baseBuilderErrorCode - 17, Properties.Resources.OBONotSupported, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition OBOExchangeFailed = new AgentErrorDefinition(baseBuilderErrorCode - 18, Properties.Resources.OBOExchangeFailed, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UnexpectedAuthorizationState = new AgentErrorDefinition(baseBuilderErrorCode - 19, Properties.Resources.OBOExchangeFailed, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition UserTokenClientNotAvailable = new AgentErrorDefinition(baseBuilderErrorCode - 20, Properties.Resources.UserTokenClientNotAvailable, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition ExchangeTokenUnexpectedNull = new AgentErrorDefinition(baseBuilderErrorCode - 21, Properties.Resources.ExchangeTokenUnexpectedNull, "https://aka.ms/AgentsSDK-Error01");

        // Extensions
        internal static AgentErrorDefinition ExtensionAlreadyRegistered = new AgentErrorDefinition(baseBuilderErrorCode - 22, Properties.Resources.ExtensionAlreadyRegistered, "https://aka.ms/AgentsSDK-Error01");
    }

}
