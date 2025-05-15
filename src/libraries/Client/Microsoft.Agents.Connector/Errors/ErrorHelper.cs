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
        /// <summary>
        /// Base error code for the authentication provider
        /// </summary>
        private static readonly int baseBuilderErrorCode = -60000;

        internal static AgentErrorDefinition InvalidAccessTokenForAgentCallback = new AgentErrorDefinition(baseBuilderErrorCode, Properties.Resources.ConversationResponseUnAuthorized, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition SendGetConversationsError = new AgentErrorDefinition(baseBuilderErrorCode - 1, Properties.Resources.GetConversationsError, "https://aka.ms/AgentsSDK-Error02");
        internal static AgentErrorDefinition SendCreateConversationError = new AgentErrorDefinition(baseBuilderErrorCode - 2, Properties.Resources.CreateConversationError, "https://aka.ms/AgentsSDK-Error03");
        internal static AgentErrorDefinition SendSendConversationError = new AgentErrorDefinition(baseBuilderErrorCode - 3, Properties.Resources.SendToConversationError, "https://aka.ms/AgentsSDK-Error04");
        internal static AgentErrorDefinition SendConversationHistoryError = new AgentErrorDefinition(baseBuilderErrorCode - 4, Properties.Resources.SendConversationHistoryError, "https://aka.ms/AgentsSDK-Error05");
        internal static AgentErrorDefinition SendUpdateActivityError = new AgentErrorDefinition(baseBuilderErrorCode - 5, Properties.Resources.SendUpdateActivityError, "https://aka.ms/AgentsSDK-Error06");
        internal static AgentErrorDefinition SendReplyToActivityError = new AgentErrorDefinition(baseBuilderErrorCode - 6, Properties.Resources.ReplyToActivityError, "https://aka.ms/AgentsSDK-Error07");
        internal static AgentErrorDefinition SendDeleteActivityError = new AgentErrorDefinition(baseBuilderErrorCode - 7, Properties.Resources.SendDeleteActivity, "https://aka.ms/AgentsSDK-Error08");
        internal static AgentErrorDefinition SendGetConversationMembersError = new AgentErrorDefinition(baseBuilderErrorCode - 8, Properties.Resources.SendGetConversationMembers, "https://aka.ms/AgentsSDK-Error09");
        internal static AgentErrorDefinition SendGetConversationMemberError = new AgentErrorDefinition(baseBuilderErrorCode - 9, Properties.Resources.SendGetConversationMember, "https://aka.ms/AgentsSDK-Error10");
        internal static AgentErrorDefinition SendDeleteConversationMemberError = new AgentErrorDefinition(baseBuilderErrorCode - 10, Properties.Resources.SendDeleteConversationMember, "https://aka.ms/AgentsSDK-Error11");
        internal static AgentErrorDefinition SendGetConversationPagedMembersError = new AgentErrorDefinition(baseBuilderErrorCode - 11, Properties.Resources.SendGetConversationPagedMembers, "https://aka.ms/AgentsSDK-Error12");
        internal static AgentErrorDefinition SendGetActivityMembersError = new AgentErrorDefinition(baseBuilderErrorCode - 12, Properties.Resources.SendGetActivityMembers, "https://aka.ms/AgentsSDK-Error13");
        internal static AgentErrorDefinition SendUploadAttachmentError = new AgentErrorDefinition(baseBuilderErrorCode - 13, Properties.Resources.SendUploadAttachment, "https://aka.ms/AgentsSDK-Error14");
        internal static AgentErrorDefinition GetSignInResourceAsync_BadRequestError = new AgentErrorDefinition(baseBuilderErrorCode - 14, Properties.Resources.GetSignInResourceAsync_BadRequest, "https://aka.ms/AgentsSDK-Error15");
        internal static AgentErrorDefinition GetAttachmentError = new AgentErrorDefinition(baseBuilderErrorCode - 15, Properties.Resources.GetAttachment_Error, "https://aka.ms/AgentsSDK-Error16");
        internal static AgentErrorDefinition GetAttachmentInfoError = new AgentErrorDefinition(baseBuilderErrorCode - 16, Properties.Resources.GetAttachmentInfoError, "https://aka.ms/AgentsSDK-Error17");
        internal static AgentErrorDefinition TokenServiceExchangeFailed = new AgentErrorDefinition(baseBuilderErrorCode - 17, Properties.Resources.TokenServiceExchangeFailed, "https://aka.ms/AgentsSDK-Error18");
        internal static AgentErrorDefinition TokenServiceExchangeUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 18, Properties.Resources.TokenServiceExchangeUnexpected, "https://aka.ms/AgentsSDK-Error19");
        internal static AgentErrorDefinition TokenServiceGetTokenUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 18, Properties.Resources.TokenServiceGetTokenUnexpected, "https://aka.ms/AgentsSDK-Error19");
        internal static AgentErrorDefinition TokenServiceGetAadTokenUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 19, Properties.Resources.TokenServiceGetAadTokenUnexpected, "https://aka.ms/AgentsSDK-Error20");
        internal static AgentErrorDefinition TokenServiceSignOutUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 20, Properties.Resources.TokenServiceSignOutUnexpected, "https://aka.ms/AgentsSDK-Error21");
        internal static AgentErrorDefinition TokenServiceGetTokenStatusUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 21, Properties.Resources.TokenServiceGetTokenStatusUnexpected, "https://aka.ms/AgentsSDK-Error22");
        internal static AgentErrorDefinition TokenServiceGetTokenOrSignInResourceUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 22, Properties.Resources.TokenServiceGetTokenOrSignInResourceUnexpected, "https://aka.ms/AgentsSDK-Error23");
        internal static AgentErrorDefinition TokenServiceGetSignInUrlUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 23, Properties.Resources.TokenServiceGetSignInUrlUnexpected, "https://aka.ms/AgentsSDK-Error24");
        internal static AgentErrorDefinition TokenServiceGetSignInResourceUnexpected = new AgentErrorDefinition(baseBuilderErrorCode - 24, Properties.Resources.TokenServiceGetSignInResourceUnexpected, "https://aka.ms/AgentsSDK-Error25");
        internal static AgentErrorDefinition TokenServiceExchangeErrorResponse = new AgentErrorDefinition(baseBuilderErrorCode - 25, Properties.Resources.TokenServiceExchangeErrorResponse, "https://aka.ms/AgentsSDK-Error26");
    }
}
