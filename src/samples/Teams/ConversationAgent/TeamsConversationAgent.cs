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
using Microsoft.Agents.Extensions.Teams.Connector;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using static Microsoft.Agents.Extensions.Teams.App.TeamsChannels.TeamsChannelAttributes;
using static Microsoft.Agents.Extensions.Teams.App.TeamsTeams.TeamsTeamAttributes;

namespace ConversationAgent;

public class TeamsConversationAgent(AgentApplicationOptions options) : AgentApplication(options)
{
    private readonly string _adaptiveCardTemplate = Path.Combine(".", "Resources", "UserMentionCardTemplate.json");

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
            new CardAction(type: ActionTypes.MessageBack, title: "Delete Card", text: "delete")
        ]
    };

    [MessageRoute]
    public static async Task SendWelcomeCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var card = NewCard("Welcome!");
        card.Buttons.Add(new CardAction
        {
            Type = ActionTypes.MessageBack,
            Title = "Update Card",
            Text = "update",
            Value = 0
        });

        await turnContext.SendActivityAsync(card.ToMessage(), cancellationToken);
    }

    [MessageRoute("update")]
    public static async Task SendUpdatedCardAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
    {
        var card = NewCard("I've been updated");

        var count = ProtocolJsonSerializer.ToObject(turnContext.Activity.Value, () => 0) + 1;
        card.Text = $"Update count - {count}";

        card.Buttons.Add(new CardAction
        {
            Type = ActionTypes.MessageBack,
            Title = "Update Card",
            Text = "update",
            Value = count
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
        string continuationToken = null;
        do
        {
            var currentPage = await TeamsInfo.GetPagedMembersAsync(turnContext, 100, continuationToken, cancellationToken);
            continuationToken = currentPage.ContinuationToken;

            foreach (var teamMember in currentPage.Members)
            {
                var createOptions = CreateConversationOptionsBuilder
                    .Create(AgentClaims.GetAppId(turnContext.Identity), Channels.Msteams, turnContext.Activity.ServiceUrl)
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

            var templateJSON = File.ReadAllText(_adaptiveCardTemplate)
                .Replace("${userName}", member.Name)
                .Replace("${userUPN}", member.Id)
                .Replace("${userAAD}", member.AadObjectId);

            var adaptiveCardAttachment = new Attachment
            {
                ContentType = "application/vnd.microsoft.card.adaptive",
                Content = templateJSON
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
