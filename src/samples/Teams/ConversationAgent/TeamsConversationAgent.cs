// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.MSTeams;
using Microsoft.Agents.Extensions.MSTeams.App;
using Microsoft.Agents.Extensions.MSTeams.Channels;
using Microsoft.Agents.Extensions.MSTeams.Teams;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Linq;

namespace ConversationAgent;

[TeamsExtension]
public partial class TeamsConversationAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    [TeamsActivityRoute(ActivityTypes.InstallationUpdate)]
    public async Task OnInstallationUpdateActivityAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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

    [TeamsMembersAddedRoute]
    public async Task OnMembersAddedAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        foreach (var teamMember in turnContext.Activity.MembersAdded)
        {
            if (teamMember.Id != turnContext.Activity.Recipient.Id && turnContext.Activity.Conversation.ConversationType != "personal")
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"Welcome to the team {teamMember.Name}."), cancellationToken);
            }
        }
    }

    [TeamsMembersRemovedRoute]
    public async Task OnMembersRemovedAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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

    [TeamsChannelCreatedRoute]
    public async Task OnChannelCreatedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.TeamsChannel channelInfo, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the Channel created");
        await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
    }

    [TeamsChannelRenamedRoute]
    public async Task OnChannelRenamedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.TeamsChannel channelInfo, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the new Channel name");
        await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
    }

    [TeamsChannelDeletedRoute]
    public async Task OnChannelDeletedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.TeamsChannel channelInfo, CancellationToken cancellationToken)
    {
        var heroCard = new HeroCard(text: $"{channelInfo.Name} is the Channel deleted");
        await turnContext.SendActivityAsync(heroCard.ToMessage(), cancellationToken);
    }

    [TeamsTeamRenamedRoute]
    public async Task OnTeamRenamedAsync(ITeamsTurnContext turnContext, ITurnState turnState, Microsoft.Teams.Apps.Schema.Team teamInfo, CancellationToken cancellationToken)
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
            new CardAction(type: ActionTypes.MessageBack, title: "Send Targeted", text: "targeted"),
            new CardAction(type: ActionTypes.MessageBack, title: "Quoted Reply", text: "quotedreply")
        ]
    };

    class CardValue
    {
        public int Count { get; set; }
    }

    [TeamsMessageRoute]
    public static async Task SendWelcomeCardAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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

    [TeamsMessageRoute("targeted")]
    public async Task SendTargetedMessagesAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var api = TeamsExtension.GetTeamsClient(turnContext);
        string? continuationToken = null;

        do
        {
            var currentPage = await api.Conversations.GetMembersPagedAsync(
                turnContext.Activity.Conversation.Id,
                100,
                continuationToken!,
                cancellationToken: cancellationToken);
            continuationToken = currentPage.ContinuationToken;

            foreach (var activity in from teamMember in currentPage.Members
                let activity = Activity.CreateMessageActivity()
                    .WithText($"{teamMember.Name}, this is a **targeted message** - only you can see this.")
                    .WithRecipient(
                        new ChannelAccount(teamMember.Id, teamMember.Name, RoleTypes.User),
                        isTargeted: true)
                select activity)
            {
                await turnContext.SendActivityAsync(activity, cancellationToken);
            }
        }
        while (continuationToken != null);
    }

    [TeamsMessageRoute("quotedreply")]
    public static async Task SendQuotedReplyAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var messageId = turnContext.Activity.Id
            ?? throw new InvalidOperationException("The incoming activity must have an ID to create a quoted reply.");
        ITeamsActivity reply = new TeamsActivity
        {
            Type = ActivityTypes.Message,
            Text = string.Empty
        };
        reply.AddQuotedReply(messageId, "This response includes a quoted reply to your message.");

        await turnContext.SendActivityAsync(reply, cancellationToken);
    }

    [TeamsMessageRoute("promptpreview")]
    public static async Task SendPromptPreviewAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var response = Activity.CreateMessageActivity()
            .WithText("This targeted response includes Prompt Preview metadata for your slash command.")
            .WithRecipient(turnContext.Activity.From, isTargeted: true);

        // TeamsTurnContext adds TargetedMessageInfoEntity when the incoming slash command is targeted.
        await turnContext.SendActivityAsync(response, cancellationToken);
    }

    [TeamsMessageRoute("update")]
    public static async Task SendUpdatedCardAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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

    [TeamsMessageRoute("whoami")]
    public async Task WhoAmIAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        try
        {
            var api = TeamsExtension.GetTeamsClient(turnContext);
            var member = await api.Conversations.GetMemberByIdAsync(
                turnContext.Activity.TeamsGetTeamInfo()?.Id!,
                turnContext.Activity.From.Id,
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("The Teams API returned an empty conversation member.");
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

        var graphClient = TeamsExtension.GetGraphClient(turnContext);
        var me = await graphClient.Me.GetAsync(cancellationToken: cancellationToken);
        await turnContext.SendActivityAsync($"Graph thinks you are: {me?.DisplayName}.", cancellationToken: cancellationToken);
    }

    [TeamsMessageRoute("delete")]
    public static async Task DeleteCardActivityAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        await turnContext.DeleteActivityAsync(turnContext.Activity.ReplyToId, cancellationToken);
    }

    [TeamsMessageRoute("messageall")]
    public async Task MessageAllMembersAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        do
        {
            var api = TeamsExtension.GetTeamsClient(turnContext);
            var currentPage = await api.Conversations.GetMembersPagedAsync(
                turnContext.Activity.TeamsGetTeamInfo()?.Id!,
                100,
                continuationToken,
                cancellationToken: cancellationToken);
            continuationToken = currentPage.ContinuationToken;

            foreach (var teamMember in currentPage.Members ?? [])
            {
                if (teamMember is null)
                {
                    continue;
                }

                var createOptions = CreateConversationOptionsBuilder
                    .Create(turnContext.Identity.GetIncomingAudience(), Microsoft.Agents.Core.Models.Channels.Msteams, turnContext.Activity.ServiceUrl)
                    .WithUser(teamMember.ToCoreChannelAccount())
                    .WithTenantId(turnContext.Activity.Conversation.TenantId)
                    .IsGroup(false)
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

    [TeamsMessageRoute("mentionme")]
    public async Task MentionAdaptiveCardActivityAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        try
        {
            var api = TeamsExtension.GetTeamsClient(turnContext);
            var member = await api.Conversations.GetMemberByIdAsync(
                turnContext.Activity.TeamsGetTeamInfo()?.Id!,
                turnContext.Activity.From.Id,
                cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("The Teams API returned an empty conversation member.");

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

    [TeamsMessageRoute("atmention")]
    public static async Task MentionActivityAsync(ITeamsTurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
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
