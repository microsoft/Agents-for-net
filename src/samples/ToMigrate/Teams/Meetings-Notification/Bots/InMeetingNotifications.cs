// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AdaptiveCards;
using AdaptiveCards.Templating;
using InMeetingNotificationsBot.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.Extensions.Teams.Connector;
using Microsoft.Agents.BotBuilder;


namespace InMeetingNotificationsBot.Bots
{
    public class InMeetingNotifications : TeamsActivityHandler
    {
        private readonly IConfiguration _config;
        private readonly MeetingAgenda _agenda;

        public InMeetingNotifications(IConfiguration configuration)
        {
            _config = configuration;
            _agenda = new MeetingAgenda
            {
                AgendaItems = new List<AgendaItem>()
                {
                     new AgendaItem { Topic = "Approve 5% dividend payment to shareholders" , Id = "1" },
                     new AgendaItem { Topic = "Increase research budget by 10%" , Id = "2"},
                     new AgendaItem { Topic = "Continue with WFH for next 3 months" , Id = "3"}
                },
            };
        }

        /// <summary>
        /// Invoked when a message activity is recieved in chat.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Value == null)
            {
                turnContext.Activity.RemoveRecipientMention();

                if (turnContext.Activity.Text.Trim() == "SendTargetedNotification")
                {
                    var meetingParticipantsList = new List<ParticipantDetail>();

                    // Get the list of members that are part of meeting group.
                    var participants = await TeamsInfo.GetPagedMembersAsync(turnContext);

                    var meetingId = turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of a MS Teams Meeting.");

                    foreach (var member in participants.Members)
                    {
                        TeamsMeetingParticipant participantDetails = await TeamsInfo.GetMeetingParticipantAsync(turnContext, meetingId, member.AadObjectId, _config["TenantId"], cancellationToken).ConfigureAwait(false);

                        // Select only those members that present when meeting is started.
                        if (participantDetails.Meeting.InMeeting == true)
                        {
                            var meetingParticipant = new ParticipantDetail() { Id = member.Id, Name = member.GivenName };
                            meetingParticipantsList.Add(meetingParticipant);
                        }
                    }

                    var meetingNotificationDetails = new MeetingNotification
                    {
                        ParticipantDetails = meetingParticipantsList
                    };

                    // Send and adaptive card to user to select members for sending targeted notifications.
                    Attachment adaptiveCardAttachment = GetAdaptiveCardAttachment("SendTargetNotificationCard.json", meetingNotificationDetails);
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(adaptiveCardAttachment), cancellationToken);
                }
                else if (turnContext.Activity.Text.Trim() == "SendInMeetingNotification")
                {
                    Attachment adaptiveCardAttachment = GetAdaptiveCardAttachment("AgendaCard.json", _agenda);
                    await turnContext.SendActivityAsync(MessageFactory.Attachment(adaptiveCardAttachment), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Please type `SendTargetedNotification` or `SendInMeetingNotification` to send In-meeting notifications."), cancellationToken);
                }
            }
            else
            {
                await HandleActions(turnContext, cancellationToken);
            }
        }

        /// <summary>
        /// Method to handle different notification actions in meeting.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private async Task HandleActions(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var action = ProtocolJsonSerializer.ToObject<ActionBase>(turnContext.Activity.Value);
            switch (action.Type)
            {
                case "PushAgenda":
                    var pushAgendaAction = ProtocolJsonSerializer.ToObject<PushAgendaAction>(turnContext.Activity.Value);
                    var agendaItem = _agenda.AgendaItems.First(a => a.Id == pushAgendaAction.Choice);
                    Console.WriteLine(pushAgendaAction);
                    Attachment adaptiveCardAttachment = GetAdaptiveCardAttachment("QuestionTemplate.json", agendaItem);
                    var activity = MessageFactory.Attachment(adaptiveCardAttachment);
                    var appId = _config["Connections:BotConnection:Settings:ClientId"];

                    activity.ChannelData = new
                    {
                        OnBehalfOf = new[]
                        {
                            new
                            {
                                ItemId = 0,
                                MentionType = "person",
                                Mri = turnContext.Activity.From.Id,
                                DisplayName = turnContext.Activity.From.Name
                            }
                        },
                        Notification = new
                        {
                            AlertInMeeting = true,
                            ExternalResourceUrl = $"https://teams.microsoft.com/l/bubble/{appId}?url=" +
                                                  HttpUtility.UrlEncode($"{_config["BaseUrl"]}/InMeetingNotificationPage?topic={agendaItem.Topic}") +
                                                  $"&height=270&width=250&title=InMeetingNotification&completionBotId={appId}"
                        }
                    };
                    await turnContext.SendActivityAsync(activity, cancellationToken);
                    break;
                case "SubmitFeedback":
                    var submitFeedback = ProtocolJsonSerializer.ToObject<SubmitFeedbackAction>(turnContext.Activity.Value);
                    var item = _agenda.AgendaItems.First(a => a.Id.ToString() == submitFeedback.Choice.ToString());
                    await turnContext.SendActivityAsync($"{turnContext.Activity.From.Name} voted **{submitFeedback.Feedback}** for '{item.Topic}'", cancellationToken: cancellationToken);
                    break;
                case "SendTargetedMeetingNotification":
                    try
                    {
                        var actionSet = ProtocolJsonSerializer.ToObject<ActionBase>(turnContext.Activity.Value);
                        var selectedMembers = actionSet.Choice;
                        var pageUrl = _config["BaseUrl"] + "/SendNotificationPage";
                        var meetingId = turnContext.Activity.TeamsGetMeetingInfo()?.Id ?? throw new InvalidOperationException("This method is only valid within the scope of a MS Teams Meeting.");
                        TargetedMeetingNotification notification = GetTargetedMeetingNotification(selectedMembers.ToString().Split(',').ToList(), pageUrl);

                        var response = await TeamsInfo.SendMeetingNotificationAsync(turnContext, notification, meetingId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Invoked when new member is added to conversation.
        /// </summary>
        /// <param name="membersAdded">List of members added to the conversation.</param>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome to Content Bubble Sample Bot! Send my hello to see today's agenda.";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }

        /// <summary>
        /// Invoked when submit activity is recieved from task module.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="taskModuleRequest">The request object for task module.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        protected override async Task<TaskModuleResponse> OnTeamsTaskModuleSubmitAsync(ITurnContext<IInvokeActivity> turnContext, TaskModuleRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            var submitFeedback = ProtocolJsonSerializer.ToObject<SubmitFeedbackAction>(taskModuleRequest.Data);
            await turnContext.SendActivityAsync($"{turnContext.Activity.From.Name} voted **{submitFeedback.Feedback}** for '{submitFeedback.Topic}'", cancellationToken: cancellationToken);

            return null;
        }

        /// <summary>
        /// Invoked when submit activity is recieved from task module.
        /// </summary>
        /// <param name="fileName">Name of the file containing adaptive card.</param>
        /// <param name="cardData">The data that needs to be binded with the card.</param>
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A an adaptive card attachment.</returns>
        private Attachment GetAdaptiveCardAttachment(string fileName, object cardData)
        {
            var templateJson = File.ReadAllText("./Cards/" + fileName);
            AdaptiveCardTemplate template = new AdaptiveCardTemplate(templateJson);

            string cardJson = template.Expand(cardData);
            AdaptiveCardParseResult result = AdaptiveCard.FromJson(cardJson);

            // Get card from result
            AdaptiveCard card = result.Card;

            var adaptiveCardAttachment = new Attachment
            {
                ContentType = AdaptiveCard.ContentType,
                Content = card.ToJson(),
            };
            return adaptiveCardAttachment;
        }

        /// <summary>
        /// Create Notification for send to recipients
        /// </summary>
        /// <param name="recipients">List of members added to the conversation.</param>
        /// <param name="pageUrl">page url that will be load in the notification.</param>
        /// <returns>Target meeting notification object.</returns>
        private TargetedMeetingNotification GetTargetedMeetingNotification(List<string> recipients, string pageUrl)
        {
            TargetedMeetingNotification notification = new TargetedMeetingNotification()
            {
                Type = "TargetedMeetingNotification",
                Value = new TargetedMeetingNotificationValue()
                {
                    Recipients = recipients,
                    Surfaces = new List<Surface>()
                    {
                        new MeetingStageSurface<TaskModuleContinueResponse>()
                        {
                            ContentType = ContentType.Task,
                            Content = new TaskModuleContinueResponse
                            {
                                Value = new TaskModuleTaskInfo()
                                {
                                    Title = "Targeted meeting Notification",
                                    Height =300,
                                    Width = 400,
                                    Url = pageUrl
                                }
                            }
                        }
                    },
                }
            };
            return notification;
        }
    }
}