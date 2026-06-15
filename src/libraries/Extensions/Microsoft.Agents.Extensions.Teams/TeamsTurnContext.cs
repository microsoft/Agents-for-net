using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.Proactive;
using Microsoft.Agents.Core.Models;
using Microsoft.Teams.Api.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable ExperimentalTeamsReactions // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

namespace Microsoft.Agents.Extensions.Teams
{
    /// <summary>
    /// Provides Teams-specific helpers for working with the current <see cref="ITurnContext"/>.
    /// </summary>
    public class TeamsTurnContext : TurnContextWrapper
    {

        private readonly Proactive _proactive;

        /// <summary>
        /// Initializes a new instance of the <see cref="TeamsTurnContext"/> class using services available on the supplied turn context.
        /// </summary>
        /// <param name="turnContext">The turn context to wrap.</param>
        /// <exception cref="Exception">Thrown when the proactive messaging service is not available on <paramref name="turnContext"/>.</exception>
        public TeamsTurnContext(ITurnContext turnContext) : base(turnContext)
        {
            this._proactive = turnContext.Services.Get<Proactive>() ?? throw new Exception("Missing Proactive service in received ITurnContext");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TeamsTurnContext"/> class using an explicit proactive messaging service.
        /// </summary>
        /// <param name="turnContext">The turn context to wrap.</param>
        /// <param name="proactive">The proactive messaging service used to create or continue conversations.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="proactive"/> is <see langword="null"/>.</exception>
        public TeamsTurnContext(ITurnContext turnContext, Proactive proactive) : base(turnContext)
        {
            this._proactive = proactive ?? throw new ArgumentNullException(nameof(proactive));
        }

        // proactive createConversation
        /// <summary>
        /// Creates a new one-on-one Teams conversation with the specified user and sends an initial message.
        /// </summary>
        /// <param name="userAccount">The Teams user account to start the conversation with.</param>
        /// <param name="text">The initial message text to send after the conversation is created.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ResourceResponse"/>.</returns>
        public async Task<ResourceResponse> CreateConversationAsync(Microsoft.Teams.Api.Account userAccount, string text, CancellationToken cancellationToken = default)
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

        /// <summary>
        /// Continues an existing Teams conversation by sending the supplied activity.
        /// </summary>
        /// <param name="conversationId">The identifier of the Teams conversation to continue.</param>
        /// <param name="activity">The activity to send to the conversation.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a <see cref="ResourceResponse"/> for the sent activity.</returns>
        public Task<ResourceResponse> ContinueConversationAsync(string conversationId, IActivity activity, CancellationToken cancellationToken = default)
        {
            var conv = new Builder.App.Proactive.Conversation(
                Identity,
                new ConversationReference(
                    agent: Activity.Recipient,
                    channelId: Channels.Msteams,
                    serviceUrl: Activity.ServiceUrl,
                    conversation: new ConversationAccount(id: conversationId)
                )
            );
            return Proactive.SendActivityAsync(
                Adapter,
                conv,
                activity,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Replies to the current activity, quoting the original Teams message when possible.
        /// </summary>
        /// <param name="text">The reply text to send.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the send operation.</param>
        /// <returns>A task that represents the asynchronous send operation. The task result contains a <see cref="ResourceResponse"/>.</returns>
        public Task<ResourceResponse> ReplyAsync(string text, CancellationToken cancellationToken = default)
        {
            if (Activity.Id != null)
            {
                var newActivity = Activity.CreateReply()
                    .AddQuote(Activity.Id, Activity.Text)
                    .AddText(text);
                return SendActivityAsync(newActivity, cancellationToken);
            }
            return SendActivityAsync(Activity.CreateReply(text), cancellationToken);
        }

        /// <summary>
        /// Adds a reaction to a Teams activity in the current conversation.
        /// </summary>
        /// <param name="reactionType">The type of reaction to add.</param>
        /// <param name="activityId">The identifier of the activity to react to. When <see langword="null"/>, the current activity identifier is used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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

        /// <summary>
        /// Removes a reaction from a Teams activity in the current conversation.
        /// </summary>
        /// <param name="reactionType">The type of reaction to remove.</param>
        /// <param name="activityId">The identifier of the activity to remove the reaction from. When <see langword="null"/>, the current activity identifier is used.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
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