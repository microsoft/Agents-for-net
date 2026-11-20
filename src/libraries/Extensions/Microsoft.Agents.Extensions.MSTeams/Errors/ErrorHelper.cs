// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Errors;
using System;

namespace Microsoft.Agents.Extensions.MSTeams.Errors
{
    /// <summary>
    /// Error helper for Teams Agent Extension
    /// This is used to setup the localized error codes for the Teams Agent Extension
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
        // Base error code for the Teams Agent Extension: -100000

        // RouteBuilders
        internal static readonly AgentErrorDefinition RouteSelectorAlreadyDefined = new AgentErrorDefinition(-100000, Properties.Resources.RouteSelectorAlreadyDefined, "https://aka.ms/M365AgentsErrorCodes/#-100000");
        internal static readonly AgentErrorDefinition RouteBuilderMissingProperty = new AgentErrorDefinition(-100001, Properties.Resources.RouteBuilderMissingProperty, "https://aka.ms/M365AgentsErrorCodes/#-100001");

        // Plans
        internal static readonly AgentErrorDefinition PlanRequiresTeamsTurn = new AgentErrorDefinition(-100002, Properties.Resources.PlanRequiresTeamsTurn, "https://aka.ms/M365AgentsErrorCodes/#-100002");
        internal static readonly AgentErrorDefinition PlanTargetedPromptNotSupported = new AgentErrorDefinition(-100003, Properties.Resources.PlanTargetedPromptNotSupported, "https://aka.ms/M365AgentsErrorCodes/#-100003");
        internal static readonly AgentErrorDefinition PlanConversationMismatch = new AgentErrorDefinition(-100004, Properties.Resources.PlanConversationMismatch, "https://aka.ms/M365AgentsErrorCodes/#-100004");
        internal static readonly AgentErrorDefinition PlanInvalidReplyChainId = new AgentErrorDefinition(-100005, Properties.Resources.PlanInvalidReplyChainId, "https://aka.ms/M365AgentsErrorCodes/#-100005");
        internal static readonly AgentErrorDefinition PlanCompletionMustBeMessage = new AgentErrorDefinition(-100006, Properties.Resources.PlanCompletionMustBeMessage, "https://aka.ms/M365AgentsErrorCodes/#-100006");
        internal static readonly AgentErrorDefinition PlanTargetedCompletionNotSupported = new AgentErrorDefinition(-100007, Properties.Resources.PlanTargetedCompletionNotSupported, "https://aka.ms/M365AgentsErrorCodes/#-100007");
        internal static readonly AgentErrorDefinition PlanCompletionOperationInvalid = new AgentErrorDefinition(-100008, Properties.Resources.PlanCompletionOperationInvalid, "https://aka.ms/M365AgentsErrorCodes/#-100008");
        internal static readonly AgentErrorDefinition PlanConnectorClientMissing = new AgentErrorDefinition(-100009, Properties.Resources.PlanConnectorClientMissing, "https://aka.ms/M365AgentsErrorCodes/#-100009");
        internal static readonly AgentErrorDefinition PlanReplyChainTransportRequired = new AgentErrorDefinition(-100010, Properties.Resources.PlanReplyChainTransportRequired, "https://aka.ms/M365AgentsErrorCodes/#-100010");
        internal static readonly AgentErrorDefinition PlanReplyChainRequestFailed = new AgentErrorDefinition(-100011, Properties.Resources.PlanReplyChainRequestFailed, "https://aka.ms/M365AgentsErrorCodes/#-100011");
        internal static readonly AgentErrorDefinition PlanInvalidLifecycleOperation = new AgentErrorDefinition(-100012, Properties.Resources.PlanInvalidLifecycleOperation, "https://aka.ms/M365AgentsErrorCodes/#-100012");

        internal static ArgumentException CreateArgumentException(AgentErrorDefinition error, string parameterName)
        {
            // The shared exception factory's string constructor does not preserve ParamName.
            return new ArgumentException(error.description, parameterName)
            {
                HResult = error.code,
                HelpLink = error.helplink
            };
        }
    }
}
