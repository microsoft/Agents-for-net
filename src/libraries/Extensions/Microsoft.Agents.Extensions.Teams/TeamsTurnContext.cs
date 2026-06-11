using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using Microsoft.Graph;
using Microsoft.Teams.Api.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable ExperimentalTeamsReactions // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


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
            var conv = new Microsoft.Agents.Builder.App.Proactive.Conversation(
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

        public Task AddReactionAsync(ReactionType reactionType, string? activityId = null, CancellationToken cancellationToken = default)
        {
            var apiClient = TeamsApiClientExtensions.GetTeamsApiClient(this);

            if (activityId == null)
            {
                activityId = Activity.Id;
            }

            return apiClient.Conversations.Reactions.AddAsync(
                conversationId: Activity.Conversation.Id,
                activityId: Activity.Id,
                reactionType: reactionType,
                cancellationToken: cancellationToken);
        }

        public Task DeleteReactionAsync(ReactionType reactionType, string? activityId = null, CancellationToken cancellationToken = default)
        {
            var apiClient = TeamsApiClientExtensions.GetTeamsApiClient(this);

            if (activityId == null)
            {
                activityId = Activity.Id;
            }

            return apiClient.Conversations.Reactions.DeleteAsync(
                conversationId: Activity.Conversation.Id,
                activityId: Activity.Id,
                reactionType: reactionType,
                cancellationToken: cancellationToken);  
        }

        /// <summary>
        /// Sends an activity to the conversation with a targeted treatment, allowing the activity to be directed to a
        /// specific recipient or group within the conversation.
        /// </summary>
        /// <remarks>This extension method adds a targeted treatment to the activity before sending it.
        /// Use this method when you need to direct an activity to a specific recipient or subset of participants in a
        /// conversation. The activity's Entities collection will be updated to include the targeted
        /// treatment.</remarks>
        /// <param name="turnContext">The context for the current conversation turn.</param>
        /// <param name="activity">The activity to send. Must represent the message or event to be delivered and cannot be null.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous send operation. The task result contains a ResourceResponse with
        /// information about the sent activity.</returns>
        public Task<ResourceResponse> SendTargetedActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
        {
            return SendActivityAsync(activity.Clone().MakeTargetedActivity(), cancellationToken);
        }

        /// <summary>
        /// Sends a set of activities to targeted recipients within the current turn context asynchronously.
        /// </summary>
        /// <remarks>Each activity is cloned and marked as targeted before being sent. Use this method
        /// when you need to deliver activities to specific recipients rather than broadcasting to all
        /// participants.</remarks>
        /// <param name="turnContext">The context for the current conversation turn.</param>
        /// <param name="activities">An array of activities to send. Each activity will be treated as targeted. Cannot be null and must not
        /// contain null elements.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous send operation. The task result contains an array of
        /// ResourceResponse objects for each sent activity.</returns>
        public Task<ResourceResponse[]> SendTargetedActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
        {
            var clonedActivities = new List<IActivity>(activities.Length);
            foreach (var activity in activities)
            {
                clonedActivities.Add(activity.Clone().MakeTargetedActivity());
            }

            return SendActivitiesAsync([.. clonedActivities], cancellationToken);
        }
    }
}