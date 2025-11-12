// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;

namespace Microsoft.Agents.Connector.Errors
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
        // Base error code for the connector: -60000

        internal static readonly AgentErrorDefinition InvalidAccessTokenForAgentCallback = new AgentErrorDefinition(-60000, Properties.Resources.ConversationResponseUnAuthorized, "https://aka.ms/M365AgentsErrorCodes/#-60000");
        internal static readonly AgentErrorDefinition SendGetConversationsError = new AgentErrorDefinition(-60001, Properties.Resources.GetConversationsError, "https://aka.ms/M365AgentsErrorCodes/#-60001");
        internal static readonly AgentErrorDefinition SendCreateConversationError = new AgentErrorDefinition(-60002, Properties.Resources.CreateConversationError, "https://aka.ms/M365AgentsErrorCodes/#-60002");
        internal static readonly AgentErrorDefinition SendSendConversationError = new AgentErrorDefinition(-60003, Properties.Resources.SendToConversationError, "https://aka.ms/M365AgentsErrorCodes/#-60003");
        internal static readonly AgentErrorDefinition SendConversationHistoryError = new AgentErrorDefinition(-60004, Properties.Resources.SendConversationHistoryError, "https://aka.ms/M365AgentsErrorCodes/#-60004");
        internal static readonly AgentErrorDefinition SendUpdateActivityError = new AgentErrorDefinition(-60005, Properties.Resources.SendUpdateActivityError, "https://aka.ms/M365AgentsErrorCodes/#-60005");
        internal static readonly AgentErrorDefinition SendReplyToActivityError = new AgentErrorDefinition(-60006, Properties.Resources.ReplyToActivityError, "https://aka.ms/M365AgentsErrorCodes/#-60006");
        internal static readonly AgentErrorDefinition SendDeleteActivityError = new AgentErrorDefinition(-60007, Properties.Resources.SendDeleteActivity, "https://aka.ms/M365AgentsErrorCodes/#-60007");
        internal static readonly AgentErrorDefinition SendGetConversationMembersError = new AgentErrorDefinition(-60008, Properties.Resources.SendGetConversationMembers, "https://aka.ms/M365AgentsErrorCodes/#-60008");
        internal static readonly AgentErrorDefinition SendGetConversationMemberError = new AgentErrorDefinition(-60009, Properties.Resources.SendGetConversationMember, "https://aka.ms/M365AgentsErrorCodes/#-60009");
        internal static readonly AgentErrorDefinition SendDeleteConversationMemberError = new AgentErrorDefinition(-60010, Properties.Resources.SendDeleteConversationMember, "https://aka.ms/M365AgentsErrorCodes/#-60010");
        internal static readonly AgentErrorDefinition SendGetConversationPagedMembersError = new AgentErrorDefinition(-60011, Properties.Resources.SendGetConversationPagedMembers, "https://aka.ms/M365AgentsErrorCodes/#-60011");
        internal static readonly AgentErrorDefinition SendGetActivityMembersError = new AgentErrorDefinition(-60012, Properties.Resources.SendGetActivityMembers, "https://aka.ms/M365AgentsErrorCodes/#-60012");
        internal static readonly AgentErrorDefinition SendUploadAttachmentError = new AgentErrorDefinition(-60013, Properties.Resources.SendUploadAttachment, "https://aka.ms/M365AgentsErrorCodes/#-60013");
        internal static readonly AgentErrorDefinition GetSignInResourceAsync_BadRequestError = new AgentErrorDefinition(-60014, Properties.Resources.GetSignInResourceAsync_BadRequest, "https://aka.ms/M365AgentsErrorCodes/#-60014");
        internal static readonly AgentErrorDefinition GetAttachmentError = new AgentErrorDefinition(-60015, Properties.Resources.GetAttachment_Error, "https://aka.ms/M365AgentsErrorCodes/#-60015");
        internal static readonly AgentErrorDefinition GetAttachmentInfoError = new AgentErrorDefinition(-60016, Properties.Resources.GetAttachmentInfoError, "https://aka.ms/M365AgentsErrorCodes/#-60016");
        internal static readonly AgentErrorDefinition TokenServiceExchangeFailed = new AgentErrorDefinition(-60017, Properties.Resources.TokenServiceExchangeFailed, "https://aka.ms/M365AgentsErrorCodes/#-60017");
        internal static readonly AgentErrorDefinition TokenServiceExchangeUnexpected = new AgentErrorDefinition(-60018, Properties.Resources.TokenServiceExchangeUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60018");
        internal static readonly AgentErrorDefinition TokenServiceGetTokenUnexpected = new AgentErrorDefinition(-60019, Properties.Resources.TokenServiceGetTokenUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60019");
        internal static readonly AgentErrorDefinition TokenServiceGetAadTokenUnexpected = new AgentErrorDefinition(-60020, Properties.Resources.TokenServiceGetAadTokenUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60020");
        internal static readonly AgentErrorDefinition TokenServiceSignOutUnexpected = new AgentErrorDefinition(-60021, Properties.Resources.TokenServiceSignOutUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60021");
        internal static readonly AgentErrorDefinition TokenServiceGetTokenStatusUnexpected = new AgentErrorDefinition(-60022, Properties.Resources.TokenServiceGetTokenStatusUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60022");
        internal static readonly AgentErrorDefinition TokenServiceGetTokenOrSignInResourceUnexpected = new AgentErrorDefinition(-60023, Properties.Resources.TokenServiceGetTokenOrSignInResourceUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60023");
        internal static readonly AgentErrorDefinition TokenServiceGetSignInUrlUnexpected = new AgentErrorDefinition(-60024, Properties.Resources.TokenServiceGetSignInUrlUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60024");
        internal static readonly AgentErrorDefinition TokenServiceGetSignInResourceUnexpected = new AgentErrorDefinition(-60025, Properties.Resources.TokenServiceGetSignInResourceUnexpected, "https://aka.ms/M365AgentsErrorCodes/#-60025");
        internal static readonly AgentErrorDefinition TokenServiceExchangeErrorResponse = new AgentErrorDefinition(-60026, Properties.Resources.TokenServiceExchangeErrorResponse, "https://aka.ms/M365AgentsErrorCodes/#-60026");
    }
}
