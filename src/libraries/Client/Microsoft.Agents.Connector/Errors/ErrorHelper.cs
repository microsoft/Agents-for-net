using Microsoft.Agents.Core.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    ///         ErrorHelper.NullIAccessTokenProvider, ex, $"{BotClaims.GetAppId(claimsIdentity)}:{serviceUrl}");
    /// 
    /// </summary>
    internal static partial class ErrorHelper
    {
        /// <summary>
        /// Base error code for the authentication provider
        /// </summary>
        private static readonly int baseBotBuilderErrorCode = -60000;

        internal static AgentErrorDefinition InvalidAccessTokenForAgentCallback = new AgentErrorDefinition(baseBotBuilderErrorCode, Properties.Resources.ConversationResponseUnAuthorized, "https://aka.ms/AgentsSDK-Error01");
        internal static AgentErrorDefinition SendGetConversationsError = new AgentErrorDefinition(baseBotBuilderErrorCode - 1, Properties.Resources.GetConversationsError, "https://aka.ms/AgentsSDK-Error02");
        internal static AgentErrorDefinition SendCreateConversationError = new AgentErrorDefinition(baseBotBuilderErrorCode - 2, Properties.Resources.CreateConversationError, "https://aka.ms/AgentsSDK-Error03");
        internal static AgentErrorDefinition SendSendConversationError = new AgentErrorDefinition(baseBotBuilderErrorCode - 3, Properties.Resources.SendToConversationError, "https://aka.ms/AgentsSDK-Error04");
        internal static AgentErrorDefinition SendConversationHistoryError = new AgentErrorDefinition(baseBotBuilderErrorCode - 4, Properties.Resources.SendConversationHistoryError, "https://aka.ms/AgentsSDK-Error05");
        internal static AgentErrorDefinition SendUpdateActivityError = new AgentErrorDefinition(baseBotBuilderErrorCode - 5, Properties.Resources.SendUpdateActivityError, "https://aka.ms/AgentsSDK-Error06");
        internal static AgentErrorDefinition SendReplyToActivityError = new AgentErrorDefinition(baseBotBuilderErrorCode - 6, Properties.Resources.ReplyToActivityError, "https://aka.ms/AgentsSDK-Error07");
        internal static AgentErrorDefinition SendDeleteActivityError = new AgentErrorDefinition(baseBotBuilderErrorCode - 7, Properties.Resources.SendDeleteActivity, "https://aka.ms/AgentsSDK-Error08");
        internal static AgentErrorDefinition SendGetConversationMembersError = new AgentErrorDefinition(baseBotBuilderErrorCode - 8, Properties.Resources.SendGetConversationMembers, "https://aka.ms/AgentsSDK-Error09");
        internal static AgentErrorDefinition SendGetConversationMemberError = new AgentErrorDefinition(baseBotBuilderErrorCode - 9, Properties.Resources.SendGetConversationMember, "https://aka.ms/AgentsSDK-Error10");
        internal static AgentErrorDefinition SendDeleteConversationMemberError = new AgentErrorDefinition(baseBotBuilderErrorCode - 10, Properties.Resources.SendDeleteConversationMember, "https://aka.ms/AgentsSDK-Error11");
        internal static AgentErrorDefinition SendGetConversationPagedMembersError = new AgentErrorDefinition(baseBotBuilderErrorCode - 11, Properties.Resources.SendGetConversationPagedMembers, "https://aka.ms/AgentsSDK-Error12");
        internal static AgentErrorDefinition SendGetActivityMembersError = new AgentErrorDefinition(baseBotBuilderErrorCode - 12, Properties.Resources.SendGetActivityMembers, "https://aka.ms/AgentsSDK-Error13");
        internal static AgentErrorDefinition SendUploadAttachmentError = new AgentErrorDefinition(baseBotBuilderErrorCode - 13, Properties.Resources.SendUploadAttachment, "https://aka.ms/AgentsSDK-Error14");
        internal static AgentErrorDefinition GetSignInResourceAsync_BadRequestError = new AgentErrorDefinition(baseBotBuilderErrorCode - 14, Properties.Resources.GetSignInResourceAsync_BadRequest, "https://aka.ms/AgentsSDK-Error15");
        internal static AgentErrorDefinition GetAttachmentError = new AgentErrorDefinition(baseBotBuilderErrorCode - 15, Properties.Resources.GetAttachment_Error, "https://aka.ms/AgentsSDK-Error16");
        internal static AgentErrorDefinition GetAttachmentInfoError = new AgentErrorDefinition(baseBotBuilderErrorCode - 16, Properties.Resources.GetAttachmentInfoError, "https://aka.ms/AgentsSDK-Error17");
    }
}
