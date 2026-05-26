using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System.Threading;
using System.Threading.Tasks;
using System;
using Microsoft.Agents.Authentication;
using Microsoft.Graph;

namespace Microsoft.Agents.Extensions.Teams
{
    public class TeamsTurnContext : TurnContextWrapper
    {

        private readonly Proactive _proactive;

        public TeamsTurnContext(ITurnContext turnContext, Proactive proactive) : base(turnContext)
        {
            this._proactive = proactive;
        }

        // proactive createConversation
        public async Task<ResourceResponse> SendActivityAsync(Microsoft.Teams.Api.Account userAccount, string text, CancellationToken cancellationToken = default)
        {
            var createOptions = CreateConversationOptionsBuilder
                .Create(Identity.GetIncomingAudience(), Channels.Msteams, Activity.ServiceUrl)
                .WithUser(userAccount.ToCoreChannelAccount())
                .WithTenantId(Activity.Conversation.TenantId)
                .IsGroup(false)
                .Build();

            await _proactive.CreateConversationAsync(
                                Adapter,
                                createOptions,
                                async (ctx, ts, ct) =>
                                {
                                    await ctx.SendActivityAsync(text, cancellationToken: ct);
                                },
                                cancellationToken: cancellationToken);

            return new ResourceResponse();
        }

        // proactive continueConversation
        public Task<ResourceResponse> SendActivityAsync(string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            Conversation conv = new Conversation(
                Identity,
                new ConversationReference(
                    agent: Activity.Recipient,
                    channelId: Channels.Msteams,
                    serviceUrl: "https://smba.trafficmanager.net/teams",
                    conversation: new ConversationAccount(id: conversationId)
                )
            );
            return Proactive.SendActivityAsync(
                Adapter,
                conv,
                activity,
                cancellationToken: cancellationToken);
        }

        public Task<ResourceResponse> ReplyAsync(string text, CancellationToken cancellationToken = default)
        {
            if (Activity.Id != null)
            {
                var newActivity = Activity.CreateReply();
                newActivity.AddQuote(Activity.Id, Activity.Text);
                newActivity.AddText(text);
                return SendActivityAsync(newActivity, cancellationToken);
            }
            return SendActivityAsync(Activity.CreateReply(text), cancellationToken);
        }
    }
}