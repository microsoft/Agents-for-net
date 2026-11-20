// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.App.UserAuth;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Graph;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.MSTeams;

public class TeamsTurnContext : TurnContextWrapper, ITeamsTurnContext
{
    private static readonly AsyncLocal<PlanSendScope?> ActivePlanSend = new();
    private readonly object _planHookLock = new();
    private bool _planBindingHookRegistered;

    public TeamsTurnContext(ITurnContext turnContext) : base(turnContext)
    {
    }

    /// <inheritdoc/>
    public new ITeamsActivity Activity =>
        _turnContext.Activity as ITeamsActivity ?? ProtocolJsonSerializer.ToObject<TeamsActivity>(_turnContext.Activity);

    /// <inheritdoc/>
    public Microsoft.Teams.Apps.Clients.ApiClient Client => _turnContext.Services.Get<Microsoft.Teams.Apps.Clients.ApiClient>();

    /// <inheritdoc/>
    [Experimental("ExperimentalTeamsPlan")]
    public TeamsPlan CreatePlan(Microsoft.Teams.Apps.PlanOptions? options = null)
        => TeamsAgentExtension.CreatePlan(this, options);

    [Experimental("ExperimentalTeamsPlan")]
    internal async Task<ResourceResponse[]> SendPlanActivityAsync(
        IActivity activity,
        ConversationReference reference,
        Func<IActivity, CancellationToken, Task<ResourceResponse>>? sendReplyChain,
        CancellationToken cancellationToken)
    {
        lock (_planHookLock)
        {
            if (!_planBindingHookRegistered)
            {
                OnSendActivities((context, activities, next) =>
                {
                    if (ActivePlanSend.Value is { } scope && ReferenceEquals(scope.Context, this))
                    {
                        foreach (var outgoing in activities.Where(HasPlanInfo))
                        {
                            outgoing.ApplyConversationReference(scope.Reference);
                        }
                    }
                    return next();
                });
                _planBindingHookRegistered = true;
            }
        }

        var previous = ActivePlanSend.Value;
        var send = new PlanSendScope(this, reference);
        ActivePlanSend.Value = send;
        try
        {
            if (sendReplyChain != null && NeedsReplyChain(activity)
                && _turnContext.Activity.DeliveryMode is not (DeliveryModes.Stream or DeliveryModes.ExpectReplies))
            {
                // Append at send time so previously registered outgoing hooks can inspect or suppress the REST call.
                lock (_planHookLock)
                {
                    OnSendActivities(async (context, activities, next) =>
                    {
                        if (!ReferenceEquals(ActivePlanSend.Value, send)
                            || context.Activity.DeliveryMode is DeliveryModes.Stream or DeliveryModes.ExpectReplies
                            || !activities.Any(NeedsReplyChain))
                        {
                            return await next().ConfigureAwait(false);
                        }

                        var responses = new ResourceResponse[activities.Count];
                        for (var index = 0; index < activities.Count; index++)
                        {
                            var outgoing = activities[index];
                            if (NeedsReplyChain(outgoing))
                            {
                                outgoing.Id = null;
                                responses[index] = await sendReplyChain(outgoing, cancellationToken).ConfigureAwait(false);
                            }
                            else
                            {
                                var ordinary = await context.Adapter.SendActivitiesAsync(
                                    context, [outgoing], cancellationToken).ConfigureAwait(false);
                                responses[index] = ordinary?.FirstOrDefault() ?? new ResourceResponse();
                            }
                            outgoing.Id = responses[index].Id;
                        }
                        return responses;
                    });
                }
            }

            return await SendActivitiesAsync([activity], cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ActivePlanSend.Value = previous;
        }

        static bool HasPlanInfo(IActivity outgoing)
            => outgoing.Entities?.Any(entity => entity.Type == Models.PlanInfoEntity.EntityName) == true;

        static bool NeedsReplyChain(IActivity outgoing)
        {
            var stream = outgoing.GetStreamingEntity();
            return outgoing.Type == ActivityTypes.Message && HasPlanInfo(outgoing)
                && !(stream?.StreamType == StreamTypes.Final && !string.IsNullOrEmpty(stream.StreamId));
        }
    }

    private sealed class PlanSendScope(TeamsTurnContext context, ConversationReference reference)
    {
        internal TeamsTurnContext Context { get; } = context;
        internal ConversationReference Reference { get; } = reference;
    }

    /// <inheritdoc/>
    public override Task<ResourceResponse> SendActivityAsync(
        string text,
        string speak = null,
        string inputHint = InputHints.AcceptingInput,
        CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(text, nameof(text));

        return SendActivityAsync(new Activity
        {
            Type = ActivityTypes.Message,
            Text = text,
            Speak = speak,
            InputHint = inputHint
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<ResourceResponse> SendActivityAsync(IActivity activity, CancellationToken cancellationToken = default)
    {
        ApplyPromptPreview(activity);

        return base.SendActivityAsync(activity, cancellationToken);
    }

    /// <inheritdoc/>
    public override Task<ResourceResponse[]> SendActivitiesAsync(IActivity[] activities, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(activities, nameof(activities));

        foreach (var activity in activities)
        {
            ApplyPromptPreview(activity);
        }

        return base.SendActivitiesAsync(activities, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        IActivity activity,
        ChannelAccount recipient,
        CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(activity, nameof(activity));

        return SendActivityAsync(activity.Clone().WithTargetedRecipient(recipient), cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        IActivity activity,
        string recipientId,
        CancellationToken cancellationToken = default)
    {
        return SendTargetedActivityAsync(
            activity,
            new ChannelAccount(recipientId, role: RoleTypes.User),
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        string text,
        ChannelAccount recipient,
        CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(text, nameof(text));

        return SendTargetedActivityAsync(
            new Activity
            {
                Type = ActivityTypes.Message,
                Text = text,
                InputHint = InputHints.AcceptingInput
            },
            recipient,
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task<ResourceResponse> SendTargetedActivityAsync(
        string text,
        string recipientId,
        CancellationToken cancellationToken = default)
    {
        return SendTargetedActivityAsync(
            text,
            new ChannelAccount(recipientId, role: RoleTypes.User),
            cancellationToken);
    }

    private void ApplyPromptPreview(IActivity activity)
    {
        if (activity?.Type == ActivityTypes.Message
            && Activity.IsRecipientTargeted()
            && !string.IsNullOrWhiteSpace(Activity.Id))
        {
            PromptPreviewActivityNormalizer.Apply(activity, Activity.Id);
        }
    }

    /// <inheritdoc/>
    public GraphServiceClient GetGraphClient(string handlerName = null, string graphBaseUrl = "https://graph.microsoft.com/v1.0")
    {
        return GraphClientFactory.CreateUserGraphClient(GetUserAuthorization(), this, handlerName, graphBaseUrl);
    }

    /// <inheritdoc/>
    public GraphServiceClient GetAppGraphClient(string graphBaseUrl = "https://graph.microsoft.com/v1.0")
    {
        var tokenProvider = GetConnections().GetTokenProvider(Identity, Activity);
        return GraphClientFactory.CreateAppGraphClient(tokenProvider, graphBaseUrl);
    }

    /// <inheritdoc/>
    public GraphServiceClient GetAppGraphClientForConnection(string connectionName, string graphBaseUrl = "https://graph.microsoft.com/v1.0")
    {
        AssertionHelpers.ThrowIfNullOrEmpty(connectionName, nameof(connectionName));
        var tokenProvider = GetConnections().GetConnection(connectionName);
        return GraphClientFactory.CreateAppGraphClient(tokenProvider, graphBaseUrl);
    }

    private UserAuthorization GetUserAuthorization()
    {
        var userAuthorization = _turnContext.Services.Get<UserAuthorization>();
        if (userAuthorization == null)
        {
            throw new InvalidOperationException(
                "UserAuthorization is not configured on the AgentApplication. A delegated (user) Graph client requires configured user authorization.");
        }

        return userAuthorization;
    }

    private IConnections GetConnections()
    {
        var connections = _turnContext.Services.Get<IConnections>();
        if (connections == null)
        {
            throw new InvalidOperationException(
                "IConnections is not configured on the AgentApplication. An app-only Graph client requires a configured token connection.");
        }

        return connections;
    }
}
