
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
        // Base error code for the builder: -50000

        internal static readonly AgentErrorDefinition NullIAccessTokenProvider = new AgentErrorDefinition(-50000, Properties.Resources.IAccessTokenProviderNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50000");
        internal static readonly AgentErrorDefinition NullUserTokenProviderIAccessTokenProvider = new AgentErrorDefinition(-50001, Properties.Resources.IAccessTokenProviderNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50001");

        // Application RouteAttribute
        internal static readonly AgentErrorDefinition AttributeSelectorNotFound = new AgentErrorDefinition(-50002, Properties.Resources.AttributeSelectorNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50002");
        internal static readonly AgentErrorDefinition AttributeSelectorInvalid = new AgentErrorDefinition(-50003, Properties.Resources.AttributeSelectorInvalid, "https://aka.ms/M365AgentsErrorCodes/#-50003");
        internal static readonly AgentErrorDefinition AttributeHandlerInvalid = new AgentErrorDefinition(-50004, Properties.Resources.AttributeHandlerInvalid, "https://aka.ms/M365AgentsErrorCodes/#-50004");
        internal static readonly AgentErrorDefinition AttributeMissingArgs = new AgentErrorDefinition(-50005, Properties.Resources.AttributeMissingArgs, "https://aka.ms/M365AgentsErrorCodes/#-50005");

        // StreamingMessages
        internal static readonly AgentErrorDefinition StreamingResponseEnded = new AgentErrorDefinition(-50006, Properties.Resources.StreamingResponseEnded, "https://aka.ms/M365AgentsErrorCodes/#-50006");

        // UserAuth (base, not Application)
        internal static readonly AgentErrorDefinition UserAuthorizationNotConfigured = new AgentErrorDefinition(-50008, Properties.Resources.UserAuthorizationNotConfigured, "https://aka.ms/M365AgentsErrorCodes/#-50008");
        internal static readonly AgentErrorDefinition UserAuthorizationRequiresAdapter = new AgentErrorDefinition(-50009, Properties.Resources.UserAuthorizationRequiresAdapter, "https://aka.ms/M365AgentsErrorCodes/#-50009");
        internal static readonly AgentErrorDefinition UserAuthorizationHandlerNotFound = new AgentErrorDefinition(-50010, Properties.Resources.UserAuthorizationHandlerNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50010");
        internal static readonly AgentErrorDefinition FailedToCreateUserAuthorizationHandler = new AgentErrorDefinition(-50011, Properties.Resources.FailedToCreateUserAuthorizationHandler, "https://aka.ms/M365AgentsErrorCodes/#-50011");
        internal static readonly AgentErrorDefinition NoUserAuthorizationHandlers = new AgentErrorDefinition(-50012, Properties.Resources.NoUserAuthorizationHandlers, "https://aka.ms/M365AgentsErrorCodes/#-50012");
        internal static readonly AgentErrorDefinition UserAuthorizationTypeNotFound = new AgentErrorDefinition(-50013, Properties.Resources.UserAuthorizationTypeNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50013");
        internal static readonly AgentErrorDefinition UserAuthorizationFailed = new AgentErrorDefinition(-50014, Properties.Resources.UserAuthorizationFailed, "https://aka.ms/M365AgentsErrorCodes/#-50014");
        internal static readonly AgentErrorDefinition UserAuthorizationAlreadyActive = new AgentErrorDefinition(-50015, Properties.Resources.UserAuthorizationAlreadyActive, "https://aka.ms/M365AgentsErrorCodes/#-50015");
        internal static readonly AgentErrorDefinition OBONotExchangeableToken = new AgentErrorDefinition(-50016, Properties.Resources.OBONotExchangeableToken, "https://aka.ms/M365AgentsErrorCodes/#-50016");
        internal static readonly AgentErrorDefinition OBONotSupported = new AgentErrorDefinition(-50017, Properties.Resources.OBONotSupported, "https://aka.ms/M365AgentsErrorCodes/#-50017");
        internal static readonly AgentErrorDefinition OBOExchangeFailed = new AgentErrorDefinition(-50018, Properties.Resources.OBOExchangeFailed, "https://aka.ms/M365AgentsErrorCodes/#-50018");
        internal static readonly AgentErrorDefinition UnexpectedAuthorizationState = new AgentErrorDefinition(-50019, Properties.Resources.OBOExchangeFailed, "https://aka.ms/M365AgentsErrorCodes/#-50019");
        internal static readonly AgentErrorDefinition UserTokenClientNotAvailable = new AgentErrorDefinition(-50020, Properties.Resources.UserTokenClientNotAvailable, "https://aka.ms/M365AgentsErrorCodes/#-50020");
        internal static readonly AgentErrorDefinition ExchangeTokenUnexpectedNull = new AgentErrorDefinition(-50021, Properties.Resources.ExchangeTokenUnexpectedNull, "https://aka.ms/M365AgentsErrorCodes/#-50021");

        // Extensions
        internal static readonly AgentErrorDefinition ExtensionAlreadyRegistered = new AgentErrorDefinition(-50022, Properties.Resources.ExtensionAlreadyRegistered, "https://aka.ms/M365AgentsErrorCodes/#-50022");

        // Agentic
        internal static readonly AgentErrorDefinition AgenticTokenProviderNotFound = new AgentErrorDefinition(-50023, Properties.Resources.IAgenticTokenProviderNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50023");
        internal static readonly AgentErrorDefinition AgenticTokenProviderFailed = new AgentErrorDefinition(-50024, Properties.Resources.AgenticTokenProviderFailed, "https://aka.ms/M365AgentsErrorCodes/#-50024");
        internal static readonly AgentErrorDefinition NotAnAgenticRequest = new AgentErrorDefinition(-50025, Properties.Resources.NotAnAgenticRequest, "https://aka.ms/M365AgentsErrorCodes/#-50025");
        // ConnectorUserAuthorization
        internal static readonly AgentErrorDefinition UnexpectedConnectorRequestToken = new AgentErrorDefinition(-50030, Properties.Resources.UnexpectedConnectorRequestToken, "https://aka.ms/M365AgentsErrorCodes/#-50030");
        internal static readonly AgentErrorDefinition UnexpectedConnectorTokenExpiration = new AgentErrorDefinition(-50031, Properties.Resources.UnexpectedConnectorTokenExpiration, "https://aka.ms/M365AgentsErrorCodes/#-50031");
        internal static readonly AgentErrorDefinition UserAuthorizationDefaultHandlerNotFound = new AgentErrorDefinition(-50032, Properties.Resources.UserAuthorizationDefaultHandlerNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50032");
        // Proactive
        internal static readonly AgentErrorDefinition ProactiveNotAllHandlersSignedIn = new AgentErrorDefinition(-50033, Properties.Resources.ProactiveNotAllHandlersSignedIn, "https://aka.ms/M365AgentsErrorCodes/#-50033");
        internal static readonly AgentErrorDefinition ProactiveConversationNotFound = new AgentErrorDefinition(-50034, Properties.Resources.ProactiveConversationNotFound, "https://aka.ms/M365AgentsErrorCodes/#-50034");
    }

}
