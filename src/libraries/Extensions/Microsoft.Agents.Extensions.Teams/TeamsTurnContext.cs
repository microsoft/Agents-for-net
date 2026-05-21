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
    class TeamsTurnContext : ITurnContext
    {

        private readonly ITurnContext _turnContext;
        private readonly Proactive _proactive;

        #region ITurnContext Contract

        public IChannelAdapter Adapter
        {
            get { return _turnContext.Adapter; }
        }

        public TurnContextStateCollection Services
        {
            get {  return _turnContext.Services; }
        }

        public TurnContextStateCollection StackState
        {
            get { return _turnContext.StackState; }
        }

        public IActivity Activity
        {
            get { return _turnContext.Activity; }
        }

        public IStreamingResponse StreamingResponse
        {
            get { return _turnContext.StreamingResponse; }
        }

        public bool Responded
        {
            get { return _turnContext.Responded; }
        }

        public ClaimsIdentity Identity
        {
            get { return _turnContext.Identity; }
        }

        public TeamsTurnContext(ITurnContext turnContext, Proactive proactive)
        {
            this._turnContext = turnContext;
            this._proactive = proactive;
        }

        public Task<ResourceResponse> SendActivityAsync(string text, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(text, speak, inputHint, cancellationToken);
        }

        public Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivityAsync(activity, cancellationToken);
        }

        public Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            return _turnContext.SendActivitiesAsync(activities, cancellationToken);
        }

        public Task<ResourceResponse> UpdateActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return _turnContext.UpdateActivityAsync(activity, cancellationToken);
        }

        public Task DeleteActivityAsync(string activityId, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(activityId, cancellationToken);
        }

        public Task DeleteActivityAsync(ConversationReference conversationReference, CancellationToken cancellationToken = default)
        {
            return _turnContext.DeleteActivityAsync(conversationReference, cancellationToken);
        }

        public ITurnContext OnSendActivities(SendActivitiesHandler handler)
        {
            _turnContext.OnSendActivities(handler);
            return this;
        }

        public ITurnContext OnUpdateActivity(UpdateActivityHandler handler)
        {
            _turnContext.OnUpdateActivity(handler);
            return this;
        }

        public ITurnContext OnDeleteActivity(DeleteActivityHandler handler)
        {
            _turnContext.OnDeleteActivity(handler);
            return this;
        }

        public Task<ResourceResponse> TraceActivityAsync(string name, object value = null, string valueType = null, [CallerMemberName] string label = null, CancellationToken cancellationToken = default)
        {
            return _turnContext.TraceActivityAsync(name, value, valueType, label, cancellationToken);
        }

        #endregion

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

        public Task<ResourceResponse> ReplyAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            
        }
         public Task<ResourceResponse> ReplyAsync(string text, string speak = null, string inputHint = "acceptingInput", CancellationToken cancellationToken = default)
        {
            return _turnContext.ReplyAsync(text, speak, inputHint, cancellationToken);
        }
}
