// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.Compat;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Connector;
using Microsoft.Teams.Api;
using Microsoft.Teams.Api.Activities.Events;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Teams.Compat
{
    /// <summary>
    /// The TeamsActivityHandler is derived from ActivityHandler. It adds support for 
    /// the Microsoft Teams specific events and interactions.
    /// </summary>
    public class TeamsActivityHandler : ActivityHandler
    {
        /// <summary>
        /// Invoked when an invoke activity is received from the connector.
        /// Invoke activities can be used to communicate many different things.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// Invoke activities communicate programmatic commands from a client or channel to an Agent.
        /// The meaning of an invoke activity is defined by the property,
        /// which is meaningful within the scope of a channel.
        /// </remarks>
        protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            try
            {
                if (turnContext.Activity.Name == null && turnContext.Activity.ChannelId == Channels.Msteams)
                {
                    return await OnTeamsCardActionInvokeAsync(turnContext, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    switch (turnContext.Activity.Name)
                    {
                        case "fileConsent/invoke":
                            return await OnTeamsFileConsentAsync(turnContext, SafeCast<FileConsentCardResponse>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false);

                        case "actionableMessage/executeAction":
                            await OnTeamsO365ConnectorCardActionAsync(turnContext, SafeCast<Microsoft.Teams.Api.O365.ConnectorCardActionQuery>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false);
                            return CreateInvokeResponse();

                        case "composeExtension/queryLink":
                            return CreateInvokeResponse(await OnTeamsAppBasedLinkQueryAsync(turnContext, SafeCast<AppBasedQueryLink>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "composeExtension/anonymousQueryLink":
                            return CreateInvokeResponse(await OnTeamsAnonymousAppBasedLinkQueryAsync(turnContext, SafeCast<AppBasedQueryLink>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "composeExtension/query":
                            return CreateInvokeResponse(await OnTeamsMessagingExtensionQueryAsync(turnContext, SafeCast<Microsoft.Teams.Api.MessageExtensions.Query>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "composeExtension/selectItem":
                            return CreateInvokeResponse(await OnTeamsMessagingExtensionSelectItemAsync(turnContext, (JsonElement) turnContext.Activity.Value, cancellationToken).ConfigureAwait(false));

                        case "composeExtension/submitAction":
                            return CreateInvokeResponse(await OnTeamsMessagingExtensionSubmitActionDispatchAsync(turnContext, SafeCast<Microsoft.Teams.Api.MessageExtensions.Action>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "composeExtension/fetchTask":
                            return CreateInvokeResponse(await OnTeamsMessagingExtensionFetchTaskAsync(turnContext, SafeCast<Microsoft.Teams.Api.MessageExtensions.Action>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "composeExtension/querySettingUrl":
                            return CreateInvokeResponse(await OnTeamsMessagingExtensionConfigurationQuerySettingUrlAsync(turnContext, SafeCast<Microsoft.Teams.Api.MessageExtensions.Query>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "composeExtension/setting":
                            await OnTeamsMessagingExtensionConfigurationSettingAsync(turnContext, (JsonElement) turnContext.Activity.Value, cancellationToken).ConfigureAwait(false);
                            return CreateInvokeResponse();

                        case "composeExtension/onCardButtonClicked":
                            await OnTeamsMessagingExtensionCardButtonClickedAsync(turnContext, (JsonElement) turnContext.Activity.Value, cancellationToken).ConfigureAwait(false);
                            return CreateInvokeResponse();

                        case "task/fetch":
                            return CreateInvokeResponse(await OnTeamsTaskModuleFetchAsync(turnContext, SafeCast<Microsoft.Teams.Api.TaskModules.Request>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "task/submit":
                            return CreateInvokeResponse(await OnTeamsTaskModuleSubmitAsync(turnContext, SafeCast<Microsoft.Teams.Api.TaskModules.Request>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));
                        
                        case "tab/fetch":
                            return CreateInvokeResponse(await OnTeamsTabFetchAsync(turnContext, SafeCast<Microsoft.Teams.Api.Tabs.Request>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));
                        
                        case "tab/submit":
                            return CreateInvokeResponse(await OnTeamsTabSubmitAsync(turnContext, SafeCast<Microsoft.Teams.Api.Tabs.Submit>(turnContext.Activity.Value), cancellationToken).ConfigureAwait(false));

                        case "config/fetch":
                            return CreateInvokeResponse(await OnTeamsConfigFetchAsync(turnContext, (JsonElement)turnContext.Activity.Value, cancellationToken).ConfigureAwait(false));

                        case "config/submit":
                            return CreateInvokeResponse(await OnTeamsConfigSubmitAsync(turnContext, (JsonElement)turnContext.Activity.Value, cancellationToken).ConfigureAwait(false));

                        default:
                            return await base.OnInvokeActivityAsync(turnContext, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (InvokeResponseException e)
            {
                return e.CreateInvokeResponse();
            }
        }

        /// <summary>
        /// Invoked when an card action invoke activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task<InvokeResponse> OnTeamsCardActionInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a signIn invoke activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected override Task OnSignInInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            return OnTeamsSigninVerifyStateAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Invoked when a signIn verify state activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsSigninVerifyStateAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a file consent card activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="fileConsentCardResponse">The response representing the value of the invoke activity sent when the user acts on
        /// a file consent card.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>An InvokeResponse depending on the action of the file consent card.</returns>
        protected virtual async Task<InvokeResponse> OnTeamsFileConsentAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            switch (fileConsentCardResponse.Action)
            {
                case "accept":
                    await OnTeamsFileConsentAcceptAsync(turnContext, fileConsentCardResponse, cancellationToken).ConfigureAwait(false);
                    return CreateInvokeResponse();

                case "decline":
                    await OnTeamsFileConsentDeclineAsync(turnContext, fileConsentCardResponse, cancellationToken).ConfigureAwait(false);
                    return CreateInvokeResponse();

                default:
                    throw new InvokeResponseException(HttpStatusCode.BadRequest, $"{fileConsentCardResponse.Action} is not a supported Action.");
            }
        }

        /// <summary>
        /// Invoked when a file consent card is accepted by the user.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="fileConsentCardResponse">The response representing the value of the invoke activity sent when the user accepts
        /// a file consent card.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsFileConsentAcceptAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a file consent card is declined by the user.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="fileConsentCardResponse">The response representing the value of the invoke activity sent when the user declines
        /// a file consent card.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsFileConsentDeclineAsync(ITurnContext<IInvokeActivity> turnContext, FileConsentCardResponse fileConsentCardResponse, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a Messaging Extension Query activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="query">The query for the search command.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Response for the query.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionQueryAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a O365 Connector Card Action activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="query">The O365 connector card HttpPOST invoke query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsO365ConnectorCardActionAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.O365.ConnectorCardActionQuery query, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when an app based link query activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="query">The invoke request body type for app-based link query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Response for the query.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedQueryLink query, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when an anonymous app based link query activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="query">The invoke request body type for app-based link query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Response for the query.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsAnonymousAppBasedLinkQueryAsync(ITurnContext<IInvokeActivity> turnContext, AppBasedQueryLink query, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a messaging extension select item activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="query">The object representing the query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Response for the query.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionSelectItemAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement query, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a Messaging Extension Fetch activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="action">The messaging extension action.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Action Response for the action.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionFetchTaskAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a messaging extension submit action dispatch activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="action">The messaging extension action.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Action Response for the action.</returns>
        protected virtual async Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionSubmitActionDispatchAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(action.BotMessagePreviewAction?.Value))
            {
                switch (action.BotMessagePreviewAction)
                {
                    case "edit":
                        return await OnTeamsMessagingExtensionAgentMessagePreviewEditAsync(turnContext, action, cancellationToken).ConfigureAwait(false);

                    case "send":
                        return await OnTeamsMessagingExtensionAgentMessagePreviewSendAsync(turnContext, action, cancellationToken).ConfigureAwait(false);

                    default:
                        throw new InvokeResponseException(HttpStatusCode.BadRequest, $"{action.BotMessagePreviewAction} is not a supported BotMessagePreviewAction.");
                }
            }
            else
            {
                return await OnTeamsMessagingExtensionSubmitActionAsync(turnContext, action, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Invoked when a messaging extension submit action activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="action">The messaging extension action.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Action Response for the action.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionSubmitActionAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a messaging extension bot message preview edit activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="action">The messaging extension action.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Action Response for the action.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionAgentMessagePreviewEditAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a messaging extension bot message preview send activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="action">The messaging extension action.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Action Response for the action.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.ActionResponse> OnTeamsMessagingExtensionAgentMessagePreviewSendAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Action action, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a messaging extension configuration query setting url activity is received from the connector.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="query">The Messaging extension query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>The Messaging Extension Response for the query.</returns>
        protected virtual Task<Microsoft.Teams.Api.MessageExtensions.Response> OnTeamsMessagingExtensionConfigurationQuerySettingUrlAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.MessageExtensions.Query query, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a configuration is set for a messaging extension.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="settings">Object representing the configuration settings.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMessagingExtensionConfigurationSettingAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement settings, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a card button is clicked in a messaging extension.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cardData">Object representing the card data.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMessagingExtensionCardButtonClickedAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement cardData, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a task module is fetched.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="taskModuleRequest">The task module invoke request value payload.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A Task Module Response for the request.</returns>
        protected virtual Task<Microsoft.Teams.Api.TaskModules.Response> OnTeamsTaskModuleFetchAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.TaskModules.Request taskModuleRequest, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a task module is submited.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="taskModuleRequest">The task module invoke request value payload.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A Task Module Response for the request.</returns>
        protected virtual Task<Microsoft.Teams.Api.TaskModules.Response> OnTeamsTaskModuleSubmitAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.TaskModules.Request taskModuleRequest, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a tab is fetched.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="tabRequest">The tab invoke request value payload.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A Tab Response for the request.</returns>
        protected virtual Task<Microsoft.Teams.Api.Tabs.Response> OnTeamsTabFetchAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.Tabs.Request tabRequest, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a tab is submitted.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="tabSubmit">The tab submit invoke request value payload.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A Tab Response for the request.</returns>
        protected virtual Task<Microsoft.Teams.Api.Tabs.Response> OnTeamsTabSubmitAsync(ITurnContext<IInvokeActivity> turnContext, Microsoft.Teams.Api.Tabs.Submit tabSubmit, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a config is fetched.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="configData">The config fetch invoke request value payload.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A Config Response for the request.</returns>
        protected virtual Task<Microsoft.Teams.Api.Config.ConfigResponse> OnTeamsConfigFetchAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement configData, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when a config is submitted.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="configData">The config fetch invoke request value payload.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A Config Response for the request.</returns>
        protected virtual Task<Microsoft.Teams.Api.Config.ConfigResponse> OnTeamsConfigSubmitAsync(ITurnContext<IInvokeActivity> turnContext, JsonElement configData, CancellationToken cancellationToken)
        {
            throw new InvokeResponseException(HttpStatusCode.NotImplemented);
        }

        /// <summary>
        /// Invoked when a conversation update activity is received from the channel.
        /// Conversation update activities are useful when it comes to responding to users being added to or removed from the channel.
        /// For example, an Agent could respond to a user being added by greeting the user.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// In a derived class, override this method to add logic that applies to all conversation update activities.
        /// </remarks>
        protected override Task OnConversationUpdateActivityAsync(ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId == Channels.Msteams)
            {
                var channelData = turnContext.Activity.GetChannelData<ChannelData>();

                if (turnContext.Activity.MembersAdded != null && turnContext.Activity.MembersAdded.Count > 0)
                {
                    return OnTeamsMembersAddedDispatchAsync(turnContext.Activity.MembersAdded, channelData?.Team, turnContext, cancellationToken);
                }

                if (turnContext.Activity.MembersRemoved != null && turnContext.Activity.MembersRemoved.Count > 0)
                {
                    return OnTeamsMembersRemovedDispatchAsync(turnContext.Activity.MembersRemoved, channelData?.Team, turnContext, cancellationToken);
                }

                if (channelData != null)
                {
                    switch (channelData.EventType)
                    {
                        case "channelCreated":
                            return OnTeamsChannelCreatedAsync(channelData.Channel, channelData.Team, turnContext, cancellationToken);

                        case "channelDeleted":
                            return OnTeamsChannelDeletedAsync(channelData.Channel, channelData.Team, turnContext, cancellationToken);

                        case "channelRenamed":
                            return OnTeamsChannelRenamedAsync(channelData.Channel, channelData.Team, turnContext, cancellationToken);

                        case "channelRestored":
                            return OnTeamsChannelRestoredAsync(channelData.Channel, channelData.Team, turnContext, cancellationToken);

                        case "teamArchived":
                            return OnTeamsTeamArchivedAsync(channelData.Team, turnContext, cancellationToken);

                        case "teamDeleted":
                            return OnTeamsTeamDeletedAsync(channelData.Team, turnContext, cancellationToken);

                        case "teamHardDeleted":
                            return OnTeamsTeamHardDeletedAsync(channelData.Team, turnContext, cancellationToken);

                        case "teamRenamed":
                            return OnTeamsTeamRenamedAsync(channelData.Team, turnContext, cancellationToken);

                        case "teamRestored":
                            return OnTeamsTeamRestoredAsync(channelData.Team, turnContext, cancellationToken);

                        case "teamUnarchived":
                            return OnTeamsTeamUnarchivedAsync(channelData.Team, turnContext, cancellationToken);

                        default:
                            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
                    }
                }
            }

            return base.OnConversationUpdateActivityAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when members other than the Agent
        /// join the channel, such as your Agent's welcome logic.
        /// UseIt will get the associated members with the provided accounts.
        /// </summary>
        /// <param name="membersAdded">A list of all the accounts added to the channel, as
        /// described by the conversation update activity.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual async Task OnTeamsMembersAddedDispatchAsync(IList<ChannelAccount> membersAdded, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var teamsMembersAdded = new List<Account>();
            foreach (var memberAdded in membersAdded)
            {
                if (memberAdded.Properties.Count > 0 || memberAdded.Id == turnContext.Activity?.Recipient?.Id)
                {
                    // when the ChannelAccount object is fully a Teams.Api.Account
                    teamsMembersAdded.Add(ProtocolJsonSerializer.ToObject<Account>(memberAdded));
                }
                else
                {
                    Account newMemberInfo = null;
                    try
                    {
                        newMemberInfo = await TeamsInfo.GetMemberAsync(turnContext, memberAdded.Id, cancellationToken).ConfigureAwait(false);
                    }
                    catch (ErrorResponseException ex)
                    {
                        if (ex.Body?.Error?.Code != "ConversationNotFound")
                        {
                            throw;
                        }

                        // unable to find the member added in ConversationUpdate Activity in the response from the GetMemberAsync call
                        newMemberInfo = new Account
                        {
                            Id = memberAdded.Id,
                            Name = memberAdded.Name,
                            AadObjectId = memberAdded.AadObjectId,
                            Role = new Role(memberAdded.Role),
                        };
                    }

                    teamsMembersAdded.Add(newMemberInfo);
                }
            }

            await OnTeamsMembersAddedAsync(teamsMembersAdded, Team, turnContext, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when members other than the Agent
        /// leave the channel, such as your Agent's good-bye logic.
        /// It will get the associated members with the provided accounts.
        /// </summary>
        /// <param name="membersRemoved">A list of all the accounts removed from the channel, as
        /// described by the conversation update activity.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMembersRemovedDispatchAsync(IList<ChannelAccount> membersRemoved, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var teamsMembersRemoved = new List<Account>();
            foreach (var memberRemoved in membersRemoved)
            {
                teamsMembersRemoved.Add(ProtocolJsonSerializer.ToObject<Account>(memberRemoved));
            }

            return OnTeamsMembersRemovedAsync(teamsMembersRemoved, Team, turnContext, cancellationToken);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when members other than the Agent
        /// join the channel, such as your Agent's welcome logic.
        /// </summary>
        /// <param name="teamsMembersAdded">A list of all the members added to the channel, as
        /// described by the conversation update activity.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMembersAddedAsync(IList<Account> teamsMembersAdded, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return OnMembersAddedAsync(ProtocolJsonSerializer.ToObject<IList<ChannelAccount>>(teamsMembersAdded), turnContext, cancellationToken);
        }

        /// <summary>
        /// Override this in a derived class to provide logic for when members other than the Agent
        /// leave the channel, such as your Agent's good-bye logic.
        /// </summary>
        /// <param name="teamsMembersRemoved">A list of all the members removed from the channel, as
        /// described by the conversation update activity.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMembersRemovedAsync(IList<Account> teamsMembersRemoved, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return OnMembersRemovedAsync(ProtocolJsonSerializer.ToObject<IList<ChannelAccount>>(teamsMembersRemoved), turnContext, cancellationToken);
        }

        /// <summary>
        /// Invoked when a Channel Created event activity is received from the connector.
        /// Channel Created correspond to the user creating a new channel.
        /// </summary>
        /// <param name="Channel">The channel info object which describes the channel.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsChannelCreatedAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Channel Deleted event activity is received from the connector.
        /// Channel Deleted correspond to the user deleting an existing channel.
        /// </summary>
        /// <param name="Channel">The channel info object which describes the channel.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsChannelDeletedAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Channel Renamed event activity is received from the connector.
        /// Channel Renamed correspond to the user renaming an existing channel.
        /// </summary>
        /// <param name="Channel">The channel info object which describes the channel.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsChannelRenamedAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Invoked when a Channel Restored event activity is received from the connector.
        /// Channel Restored correspond to the user restoring a previously deleted channel.
        /// </summary>
        /// <param name="Channel">The channel info object which describes the channel.</param>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsChannelRestoredAsync(Channel Channel, Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Team Archived event activity is received from the connector.
        /// Team Archived correspond to the user archiving a team.
        /// </summary>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsTeamArchivedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Team Deleted event activity is received from the connector.
        /// Team Deleted corresponds to the user deleting a team.
        /// </summary>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsTeamDeletedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Team Hard Deleted event activity is received from the connector.
        /// Team Hard Deleted corresponds to the user hard deleting a team.
        /// </summary>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsTeamHardDeletedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Team Renamed event activity is received from the connector.
        /// Team Renamed correspond to the user renaming an existing team.
        /// </summary>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsTeamRenamedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Team Restored event activity is received from the connector.
        /// Team Restored corresponds to the user restoring a team.
        /// </summary>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsTeamRestoredAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Team Unarchived event activity is received from the connector.
        /// Team Unarchived correspond to the user unarchiving a team.
        /// </summary>
        /// <param name="Team">The team info object representing the team.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsTeamUnarchivedAsync(Team Team, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when an event activity is received from the channel.
        /// Event activities can be used to communicate many different things.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// In a derived class, override this method to add logic that applies to all event activities.
        /// </remarks>
        protected override Task OnEventActivityAsync(ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId == Channels.Msteams)
            {
                switch (turnContext.Activity.Name)
                {
                    case "application/vnd.microsoft.readReceipt":
                        return OnTeamsReadReceiptAsync(ProtocolJsonSerializer.ToObject<ReadReceiptInfo>(turnContext.Activity.Value), turnContext, cancellationToken);
                    case "application/vnd.microsoft.meetingStart":
                        return OnTeamsMeetingStartAsync(ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.MeetingDetails>(turnContext.Activity.Value), turnContext, cancellationToken);
                    case "application/vnd.microsoft.meetingEnd":
                        return OnTeamsMeetingEndAsync(ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.Meetings.MeetingDetails>(turnContext.Activity.Value), turnContext, cancellationToken);
                    case "application/vnd.microsoft.meetingParticipantJoin":
                        return OnTeamsMeetingParticipantsJoinAsync(ProtocolJsonSerializer.ToObject<MeetingParticipantJoinActivityValue>(turnContext.Activity.Value), turnContext, cancellationToken);
                    case "application/vnd.microsoft.meetingParticipantLeave":
                        return OnTeamsMeetingParticipantsLeaveAsync(ProtocolJsonSerializer.ToObject<MeetingParticipantLeaveActivityValue>(turnContext.Activity.Value), turnContext, cancellationToken);
                }
            }

            return base.OnEventActivityAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Invoked when a Teams Meeting Start event activity is received from the connector.
        /// Override this in a derived class to provide logic for when a meeting is started.
        /// </summary>
        /// <param name="meeting">The details of the meeting.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMeetingStartAsync(Microsoft.Teams.Api.Meetings.MeetingDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Teams Meeting End event activity is received from the connector.
        /// Override this in a derived class to provide logic for when a meeting is ended.
        /// </summary>
        /// <param name="meeting">The details of the meeting.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMeetingEndAsync(Microsoft.Teams.Api.Meetings.MeetingDetails meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a read receipt for a previously sent message is received from the connector.
        /// Override this in a derived class to provide logic for when the Agent receives a read receipt event.
        /// </summary>
        /// <param name="readReceiptInfo">Information regarding the read receipt. i.e. Id of the message last read by the user.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsReadReceiptAsync(ReadReceiptInfo readReceiptInfo, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Teams Participants Join event activity is received from the connector.
        /// Override this in a derived class to provide logic for when meeting participants are added.
        /// </summary>
        /// <param name="meeting">The details of the meeting.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMeetingParticipantsJoinAsync(MeetingParticipantJoinActivityValue meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a Teams Participants Leave event activity is received from the connector.
        /// Override this in a derived class to provide logic for when meeting participants are removed.
        /// </summary>
        /// <param name="meeting">The details of the meeting.</param>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMeetingParticipantsLeaveAsync(MeetingParticipantLeaveActivityValue meeting, ITurnContext<IEventActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when an message update activity is received.
        /// <see cref="ActivityTypes.MessageUpdate"/> activities, such as the conversational logic.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// In a derived class, override this method to add logic that applies to all message update activities.
        /// </remarks>
        protected override Task OnMessageUpdateActivityAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId == Channels.Msteams)
            {
                var channelData = turnContext.Activity.GetChannelData<ChannelData>();

                if (channelData != null)
                {
                    switch (channelData.EventType)
                    {
                        case "editMessage":
                            return OnTeamsMessageEditAsync(turnContext, cancellationToken);

                        case "undeleteMessage":
                            return OnTeamsMessageUndeleteAsync(turnContext, cancellationToken);

                        default:
                            return base.OnMessageUpdateActivityAsync(turnContext, cancellationToken);
                    }
                }
            }

            return base.OnMessageUpdateActivityAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Invoked when an message delete activity is received.
        /// <see cref="ActivityTypes.MessageDelete"/> activities, such as the conversational logic.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>
        /// In a derived class, override this method to add logic that applies to all message update activities.
        /// </remarks>
        protected override Task OnMessageDeleteActivityAsync(ITurnContext<IMessageDeleteActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.ChannelId == Channels.Msteams)
            {
                var channelData = turnContext.Activity.GetChannelData<ChannelData>();

                if (channelData != null)
                {
                    switch (channelData.EventType)
                    {
                        case "softDeleteMessage":
                            return OnTeamsMessageSoftDeleteAsync(turnContext, cancellationToken);

                        default:
                            return base.OnMessageDeleteActivityAsync(turnContext, cancellationToken);
                    }
                }
            }

            return base.OnMessageDeleteActivityAsync(turnContext, cancellationToken);
        }

        /// <summary>
        /// Invoked when a edit message event activity is received.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMessageEditAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a undo soft delete message event activity is received.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMessageUndeleteAsync(ITurnContext<IMessageUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Invoked when a soft delete message event activity is received.
        /// </summary>
        /// <param name="turnContext">A strongly-typed context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected virtual Task OnTeamsMessageSoftDeleteAsync(ITurnContext<IMessageDeleteActivity> turnContext, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Safely casts an object to an object of type <typeparamref name="T"/> .
        /// </summary>
        /// <param name="value">The object to be casted.</param>
        /// <returns>The object casted in the new type.</returns>
        private static T SafeCast<T>(object value)
        {
            if (value is not JsonElement)
            {
                throw new InvokeResponseException(HttpStatusCode.BadRequest, $"expected type '{value.GetType().Name}'");
            }

            return ProtocolJsonSerializer.ToObject<T>(value);
        }
    }
}
