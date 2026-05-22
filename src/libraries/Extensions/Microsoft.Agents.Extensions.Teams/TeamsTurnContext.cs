using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Teams;
using Microsoft.Agents.Extensions.Teams.TeamsChannels;
using Microsoft.Agents.Extensions.Teams.TeamsTeams;
using System.Xml;

namespace Microsoft.Agents.Extensions.Teams
{
    class TeamsTurnContext : TurnContextWrapper
    {

        private readonly Proactive _proactive;

        public TeamsTurnContext(ITurnContext turnContext, Proactive proactive) : base(turnContext)
        {
            this._proactive = proactive;
        }

        private async Task<ResourceResponse> SendActivityToUserAsync(Microsoft.Teams.Api.Account userAccount, IActivity activity, CancellationToken cancellationToken = default)
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
                        await ctx.SendActivityAsync(activity, ct);
                    },
                    cancellationToken: cancellationToken);
            return new ResourceResponse();
        }

        public Task<ResourceResponse> SendActivityAsync(Microsoft.Teams.Api.Account userAccount, IActivity activity, CancellationToken cancellationToken = default)
        {
            return SendActivityToUserAsync(userAccount, activity, cancellationToken);
        }

        public Task<ResourceResponse> SendActivityAsync(string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            return _proactive.SendActivityAsync(
                Adapter,
                conversationId,
                activity,
                cancellationToken: cancellationToken);
        }

        public Task<ResourceResponse> ReplyAsync(string text, CancellationToken cancellationToken = default)
        {
            if (Activity.Id != null)
            {
                var newActivity = Activity.CreateReply();
                newActivity.AddQuote(Activity.Id, text);
                return SendActivityAsync(newActivity, cancellationToken);
            }
            return SendActivityAsync(Activity.CreateReply(text), cancellationToken);
        }
        public Task<ResourceResponse> ReplyAsync(string text, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default)
        {
            return _turnContext.ReplyAsync(text, speak, inputHint, cancellationToken);
        }
}
