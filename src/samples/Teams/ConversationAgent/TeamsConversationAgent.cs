// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.TeamsChannels;
using Microsoft.Agents.Extensions.Teams.TeamsTeams;
using Microsoft.Teams.Api.Clients;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace ConversationAgent;

[TeamsExtension]
public partial class TeamsConversationAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [ActivityRoute(ActivityTypes.InstallationUpdate)]
    public async Task OnInstallationUpdateActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        if (turnContext.Activity.Conversation.ConversationType == "channel")
        {
            await turnContext.SendActivityAsync($"Welcome to Microsoft Teams conversationUpdate events demo. This agent is configured in {turnContext.Activity.Conversation.Name}", cancellationToken: cancellationToken);
        }
        else
        {
            await turnContext.SendActivityAsync("Welcome to Microsoft Teams conversationUpdate events demo.", cancellationToken: cancellationToken);
        }
    }

    [MembersAddedRoute]
    public async Task OnMembersAddedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (var teamMember in turnContext.Activity.MembersAdded)
        {
            if (teamMember.Id != turnContext.Activity.Recipient.Id && turnContext.Activity.Conversation.ConversationType != "personal")
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the team {teamMember.Name}."), cancellationToken);
            }
        }
    }

    [MembersRemovedRoute]
    public async Task OnMembersRemovedAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (var member in turnContext.Activity.MembersRemoved)
        {
            if (member.Id == turnContext.Activity.Recipient.Id)
            {
                // The bot was removed
                // You should clear any cached data you have for this team
            }
            else
            {
                var team = turnContext.Activity.TeamsGetTeamInfo();
                var heroCard = new HeroCard(text: $"{member.Name} was removed from {team.Name}");
                await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
            }
        }
    }

    [ChannelCreatedRoute]
    public async Task OnChannelCreatedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Channel channelInfo, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the Channel created");
        await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
    }

    [ChannelRenamedRoute]
    public async Task OnChannelRenamedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Channel channelInfo, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the new Channel name");
        await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
    }

    [ChannelDeletedRoute]
    public async Task OnChannelDeletedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Channel channelInfo, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the Channel deleted");
        await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
    }

    [TeamRenamedRoute]
    public async Task OnTeamRenamedAsync(ITurnContext turnContext, ITurnState turnState, Microsoft.Teams.Api.Team teamInfo, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{teamInfo.Name} is the new Team name");
        await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
    }

    private static HeroCard NewCard(string title) => new(title: title)
    {
        Buttons =
        [
            new CardAction(type: ActionTypes.MessageBack, title: "Message all members", text: "messageall"),
            new CardAction(type: ActionTypes.MessageBack, title: "Who am I?", text: "whoami"),
            new CardAction(type: ActionTypes.MessageBack, title: "Mention Me", text: "mentionme"),
            new CardAction(type: ActionTypes.MessageBack, title: "Delete Card", text: "delete"),
            new CardAction(type: ActionTypes.MessageBack, title: "Send Targeted", text: "targeted")
        ]
    };

    class CardValue
    {
        public int Count { get; set; }
    }

    [MessageRoute]
    public static async Task SendWelcomeCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var card = NewCard("Welcome!");
        card.Buttons.Add(new CardAction
        {
            Type = ActionTypes.MessageBack,
            Title = "Update Card",
            Text = "update",
            Value = new CardValue { Count = 0 }
        });

        await turnContext.SendActivityAsync(card.ToMessage(), cancellationToken);
    }

    [MessageRoute("targeted")]
    public static async Task SendTargetedMessagesAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var api = turnContext.Services.Get<ApiClient>();
        var members = await api.Conversations.Members.GetAsync(turnContext.Activity.Conversation.Id, cancellationToken);

        foreach (var member in members)
        {
            var activity = new Activity
            {
                Type = ActivityTypes.Message,
                Text = $"👋 {member.Name} This is a **targeted message** - only YOU can see this!",
                Recipient = new ChannelAccount() { Id = member.Id, Name = member.Name, Role = RoleTypes.User }
            };

            await turnContext.SendTargetedActivityAsync(activity, cancellationToken);
        }
    }

    [MessageRoute("update")]
    public static async Task SendUpdatedCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var card = NewCard("I've been updated");

        var cardValue = ProtocolJsonSerializer.ToObject<CardValue>(turnContext.Activity.Value, () => new CardValue { Count = 0 });
        cardValue.Count++;
        card.Text = $"Update count - {cardValue.Count}";

        card.Buttons.Add(new CardAction
        {
            Type = ActionTypes.MessageBack,
            Title = "Update Card",
            Text = "update",
            Value = cardValue
        });

        var activity = card.ToMessage();
        activity.Id = turnContext.Activity.ReplyToId;

        await turnContext.UpdateActivityAsync(activity, cancellationToken);
    }

    [MessageRoute("whoami")]
    public static async Task WhoAmIAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        try
        {
            var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);
            await turnContext.SendActivityAsync($"You are: {member.Name}.", cancellationToken: cancellationToken);
        }
        catch (ErrorResponseException e)
        {
            if (e.Body.Error.Code.Equals("MemberNotFoundInConversation", StringComparison.OrdinalIgnoreCase))
            {
                await turnContext.SendActivityAsync("Member not found.", cancellationToken: cancellationToken);
                return;
            }
            else
            {
                throw;
            }
        }
    }

    [MessageRoute("delete")]
    public static async Task DeleteCardActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.DeleteActivityAsync(turnContext.Activity.ReplyToId, cancellationToken);
    }

    [MessageRoute("messageall")]
    public async Task MessageAllMembersAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        do
        {
            var currentPage = await TeamsInfo.GetPagedMembersAsync(turnContext, 100, continuationToken!, cancellationToken);
            continuationToken = currentPage.ContinuationToken;

            foreach (var teamMember in currentPage.Members)
            {
                var createOptions = CreateConversationOptionsBuilder
                    .Create(turnContext.Identity.GetIncomingAudience(), Channels.Msteams, turnContext.Activity.ServiceUrl)
                    .WithUser(teamMember.ToCoreChannelAccount())
                    .WithTenantId(turnContext.Activity.Conversation.Id)
                    .IsGroup(true)
                    .Build();

                await Proactive.CreateConversationAsync(
                    turnContext.Adapter, 
                    createOptions,
                    async (ctx, ts, ct) =>
                    {
                        await ctx.SendActivityAsync($"Hello {teamMember.Name}. I'm a Teams agent.", cancellationToken: ct);
                    },
                    cancellationToken: cancellationToken);
            }
        }
        while (continuationToken != null);

        await turnContext.SendActivityAsync("All messages have been sent.", cancellationToken: cancellationToken);
    }

    [MessageRoute("mentionme")]
    public async Task MentionAdaptiveCardActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        try
        {
            var member = await TeamsInfo.GetMemberAsync(turnContext, turnContext.Activity.From.Id, cancellationToken);

            var card = new Microsoft.Teams.Cards.AdaptiveCard([
                new Microsoft.Teams.Cards.TextBlock($"Mention a user by User Principle Name: Hello <at>${member.Name} UPN</at>"),
                new Microsoft.Teams.Cards.TextBlock($"Mention a user by AAD Object Id: Hello <at>${member.Name} AAD</at>"),
            ])
            {
                Msteams = new Microsoft.Teams.Cards.TeamsCardProperties()
                {
                    Entities =
                    [
                        new Microsoft.Teams.Cards.Mention
                        {
                            Mentioned = new Microsoft.Teams.Cards.MentionedEntity()
                            {
                                Id = member.Id,
                                Name = member.Name
                            },
                            Text = $"<at>{XmlConvert.EncodeName(member.Name)} UPN</at>"
                        },
                        new Microsoft.Teams.Cards.Mention
                        {
                            Mentioned = new Microsoft.Teams.Cards.MentionedEntity()
                            {
                                Id = member.AadObjectId,
                                Name = member.Name
                            },
                            Text = $"<at>{XmlConvert.EncodeName(member.Name)} AAD</at>"
                        }
                    ]
                }
            };

            var adaptiveCardAttachment = new Attachment
            {
                ContentType = ContentTypes.AdaptiveCard,
                Content = card
            };

            await turnContext.SendActivityAsync(MessageFactory.Attachment(adaptiveCardAttachment), cancellationToken);
        }
        catch (ErrorResponseException e)
        {
            if (e.Body.Error.Code.Equals("MemberNotFoundInConversation", StringComparison.OrdinalIgnoreCase))
            {
                await turnContext.SendActivityAsync("Member not found.", cancellationToken: cancellationToken);
                return;
            }
            else
            {
                throw;
            }
        }
    }

    [MessageRoute("atmention")]
    public static async Task MentionActivityAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var mention = new Mention
        {
            Mentioned = turnContext.Activity.From,
            Text = $"<at>{XmlConvert.EncodeName(turnContext.Activity.From.Name)}</at>",
        };

        var replyActivity = MessageFactory.Text($"Hello {mention.Text}.");
        replyActivity.Entities = [mention];

        await turnContext.SendActivityAsync(replyActivity, cancellationToken);
    }
}
