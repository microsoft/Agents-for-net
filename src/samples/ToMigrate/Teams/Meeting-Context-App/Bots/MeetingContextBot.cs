// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.BotBuilder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Teams.Compat;
using Microsoft.Agents.Extensions.Teams.Connector;
using Microsoft.Agents.Extensions.Teams.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MeetingContextApp.Bots
{
    public class MeetingContextBot : TeamsActivityHandler
    {
        public const string CommandString = "Please use one of these two commands: **Meeting Context** or **Participant Context**";
        
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text != null)
            {
                var text = turnContext.Activity.RemoveRecipientMention();
                if (text.ToLower().Contains("participant context"))
                {
                    var channelDataObject = turnContext.Activity.GetChannelData<TeamsChannelData>();

                    var tenantId = channelDataObject.Tenant.Id;
                    var meetingId = channelDataObject.Meeting.Id;
                    var participantId = turnContext.Activity.From.AadObjectId;

                    // GetMeetingParticipant
                    TeamsMeetingParticipant participantDetails = await TeamsInfo.GetMeetingParticipantAsync(turnContext, meetingId, participantId, tenantId, cancellationToken: cancellationToken);

                    var formattedString = GetFormattedSerializeObject(participantDetails);

                    await turnContext.SendActivityAsync(MessageFactory.Text(formattedString), cancellationToken);
                }
                else if (text.ToLower().Contains("meeting context"))
                {
                    MeetingInfo meetingInfo = await TeamsInfo.GetMeetingInfoAsync(turnContext, cancellationToken: cancellationToken);

                    var formattedString = GetFormattedSerializeObject(meetingInfo);

                    await turnContext.SendActivityAsync(MessageFactory.Text(formattedString), cancellationToken);
                }
                else
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(CommandString), cancellationToken);
                }
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Hello and Welcome!"), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Text(CommandString), cancellationToken);
        }

        /// <summary>
        /// Gets the serialize formatted object string.
        /// </summary>
        /// <param name="obj">Incoming object needs to be formatted.</param>
        /// <returns>Formatted string.</returns>
        private static string GetFormattedSerializeObject (object obj)
        {
            var formattedString = "";
            foreach (var meetingDetails in obj.GetType().GetProperties())
            {
                var detail = meetingDetails.GetValue(obj, null);
                var block = $"<b>{meetingDetails.Name}:</b> <br>";
                var storeTemporaryFormattedString = "";

                if (detail != null)
                {
                    if (detail.GetType().Name != "String")
                    {
                        foreach (var value in detail.GetType().GetProperties())
                        {
                            storeTemporaryFormattedString += $" <b> &nbsp;&nbsp;{value.Name}:</b> {value.GetValue(detail, null)}<br/>";
                        }

                        Console.WriteLine(storeTemporaryFormattedString);

                        formattedString += block + storeTemporaryFormattedString;
                        storeTemporaryFormattedString = String.Empty;
                    }
                }
            }

            return formattedString;
        }
    }
}
