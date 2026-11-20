// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Connector;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Teams.Apps;
using Microsoft.Teams.Apps.Schema;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PlanInfoEntity = Microsoft.Agents.Extensions.MSTeams.Models.PlanInfoEntity;
using PlanStep = Microsoft.Teams.Apps.Schema.Entities.PlanStep;
using PlanType = Microsoft.Teams.Apps.Schema.Entities.PlanType;
using PlanTypes = Microsoft.Teams.Apps.Schema.Entities.PlanTypes;
using ErrorHelper = Microsoft.Agents.Extensions.MSTeams.Errors.ErrorHelper;

namespace Microsoft.Agents.Extensions.MSTeams;

/// <summary>
/// Explicit, prompt-bound Teams Plan output using Core activities and the current turn's transport.
/// </summary>
/// <remarks>
/// Applications perform the work and own each step's versions. This helper does not execute a
/// workflow or cancel operations. For delayed work, persist
/// <see cref="Microsoft.Agents.Extensions.MSTeams.TeamsPlan.Options"/> and reconstruct the helper in
/// an authenticated continuation for the same conversation. Do not persist the helper or its turn context.
/// Targeted Plans are not supported.
/// </remarks>
[Experimental("ExperimentalTeamsPlan")]
public sealed class TeamsPlan
{
    private readonly TeamsTurnContext _turnContext;

    /// <summary>
    /// Binds a Plan to the incoming prompt, or restores a saved binding in a fresh continuation.
    /// Sends nothing.
    /// </summary>
    /// <param name="turnContext">The current Teams turn with a service-scoped Core connector client.</param>
    /// <param name="options">The saved binding, or null to bind to the incoming activity's own ID.</param>
    public TeamsPlan(ITurnContext turnContext, PlanOptions? options = null)
    {
        AssertionHelpers.ThrowIfNull(turnContext, nameof(turnContext));
        if (turnContext.Activity.ChannelId != Core.Models.Channels.Msteams)
        {
            throw ErrorHelper.CreateArgumentException(ErrorHelper.PlanRequiresTeamsTurn, nameof(turnContext));
        }
        if (turnContext.Activity.IsTargetedActivity()
            || (turnContext.Activity.Recipient?.Properties?.TryGetValue("isTargeted", out var targeted) == true
                && targeted.ValueKind == JsonValueKind.True))
        {
            throw ErrorHelper.CreateArgumentException(ErrorHelper.PlanTargetedPromptNotSupported, nameof(turnContext));
        }

        _turnContext = turnContext as TeamsTurnContext ?? new TeamsTurnContext(turnContext);
        Options = options ?? CreateOptions(turnContext.Activity);
        AssertionHelpers.ThrowIfNullOrWhiteSpace(Options.ConversationId, nameof(options));
        AssertionHelpers.ThrowIfNullOrWhiteSpace(Options.ReplyToId, nameof(options));
        if (Options.ConversationId != turnContext.Activity.Conversation?.Id)
        {
            throw ErrorHelper.CreateArgumentException(ErrorHelper.PlanConversationMismatch, nameof(options));
        }
        if (Options.ReplyChain is { } replyChain)
        {
            ValidateReplyChainId(replyChain.ChatOrChannelId, nameof(options));
            ValidateReplyChainId(replyChain.ReplyChainId, nameof(options));
        }
    }

    /// <summary>
    /// Gets the serializable original prompt/conversation binding. Runtime clients and credentials are not included.
    /// </summary>
    public PlanOptions Options { get; }

    /// <summary>
    /// Raised after a successful send. Typing acknowledgements have no message response.
    /// </summary>
    public event EventHandler<TeamsPlanSentEventArgs>? Sent;

    /// <summary>
    /// Starts a Plan with the supplied step revisions.
    /// </summary>
    public Task StartAsync(IEnumerable<PlanStep> steps, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(steps, nameof(steps));
        return SendUpdateAsync(PlanTypes.Start, steps, cancellationToken);
    }

    /// <summary>
    /// Adds or updates only the supplied steps. Applications increment each changed step's version.
    /// </summary>
    public Task UpdateAsync(IEnumerable<PlanStep> steps, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(steps, nameof(steps));
        return SendUpdateAsync(PlanTypes.Continue, steps, cancellationToken);
    }

    /// <summary>
    /// Reports that the Plan has stopped. Does not cancel application work.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken = default)
        => SendUpdateAsync(PlanTypes.Stop, null, cancellationToken);

    /// <summary>
    /// Reports a Plan-level failure. Does not cancel application work.
    /// </summary>
    public Task ErrorAsync(CancellationToken cancellationToken = default)
        => SendUpdateAsync(PlanTypes.Error, null, cancellationToken);

    /// <summary>
    /// Completes the Plan by posting a final Core message.
    /// </summary>
    public Task<ResourceResponse> CompleteAsync(string message, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(message, nameof(message));
        return CompleteAsync(new Activity { Type = ActivityTypes.Message, Text = message }, cancellationToken);
    }

    /// <summary>
    /// Posts a completion message, preserving caller content without mutating the supplied activity.
    /// </summary>
    /// <remarks>Service conflicts propagate as <see cref="Microsoft.Agents.Core.Errors.ErrorResponseException"/> and are not retried.</remarks>
    public async Task<ResourceResponse> CompleteAsync(IActivity message, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync(PrepareCompletion(message), completion: true, cancellationToken).ConfigureAwait(false);
        if (response != null)
        {
            Sent?.Invoke(this, new TeamsPlanSentEventArgs(response));
        }
        return response ?? new ResourceResponse();
    }

    /// <summary>
    /// Ends the current streaming response by posting the composed final message as this Plan's completion.
    /// </summary>
    /// <remarks>
    /// Preserves the streaming response's text, cards, citations, and labels without requiring
    /// <see cref="Microsoft.Agents.Builder.IStreamingResponse.FinalMessage"/>.
    /// A recognized streaming-timeout rejection permits one ordinary completion POST.
    /// Service conflicts and ambiguous failures propagate without retry.
    /// </remarks>
    public Task<StreamingResponseResult> CompleteStreamingAsync(CancellationToken cancellationToken = default)
        => _turnContext.StreamingResponse.EndStreamAsync(SendStreamingCompletionAsync, cancellationToken);

    private async Task<ResourceResponse> SendStreamingCompletionAsync(IActivity finalActivity, CancellationToken cancellationToken)
    {
        var activity = PrepareCompletion(finalActivity);
        ResourceResponse response;
        try
        {
            response = await PostAsync(activity, completion: true, cancellationToken).ConfigureAwait(false);
        }
        catch (ErrorResponseException error) when (
            activity.GetStreamingEntity()?.StreamType == StreamTypes.Final
            && string.Equals(error.Body?.Error?.Code, "ContentStreamNotAllowed", StringComparison.OrdinalIgnoreCase)
            && string.Equals(error.Body?.Error?.Message, "Content stream finished due to exceeded streaming time.", StringComparison.OrdinalIgnoreCase))
        {
            activity.Entities.Remove(activity.GetStreamingEntity());
            activity.Id = null;
            response = await PostAsync(activity, completion: true, cancellationToken).ConfigureAwait(false);
        }

        if (response != null)
        {
            Sent?.Invoke(this, new TeamsPlanSentEventArgs(response));
        }
        return response ?? new ResourceResponse();
    }

    private static PlanOptions CreateOptions(IActivity activity)
    {
        var channelData = activity.GetChannelData<TeamsChannelData>();
        var chatId = channelData?.Chat?.Id;
        var rootId = channelData?.ReplyChain?.Id;
        return new PlanOptions
        {
            ConversationId = activity.Conversation?.Id!,
            ReplyToId = activity.Id!,
            ReplyChain = channelData?.Channel == null
                && activity.Conversation?.ConversationType != "channel"
                && !string.IsNullOrWhiteSpace(chatId)
                && !string.IsNullOrWhiteSpace(rootId)
                && IsInsideChatThread(activity, channelData, rootId)
                ? new PlanReplyChain { ChatOrChannelId = chatId, ReplyChainId = rootId }
                : null
        };
    }

    private static bool IsInsideChatThread(IActivity activity, TeamsChannelData? channelData, string rootId)
    {
        bool IsOtherMessageId(string? id) => !string.IsNullOrEmpty(id) && id != rootId
            && id.All(char.IsAsciiDigit) && id.Any(character => character != '0');

        switch (activity.Type)
        {
            case ActivityTypes.Message:
                return string.IsNullOrEmpty(channelData?.Source?.Name)
                    ? activity.Id != rootId
                    : IsOtherMessageId(activity.ReplyToId);
            case ActivityTypes.MessageUpdate:
            case ActivityTypes.MessageDelete:
                return activity.Id != rootId;
            case ActivityTypes.MessageReaction:
                return IsOtherMessageId(activity.ReplyToId);
            case ActivityTypes.Invoke:
                var messageId = activity.ReplyToId;
                if (activity.Value is not null and not string)
                {
                    var value = ProtocolJsonSerializer.ToJsonElement(activity.Value);
                    if (value.ValueKind == JsonValueKind.Object
                        && value.TryGetProperty("messagePayload", out var payload) && payload.ValueKind == JsonValueKind.Object
                        && payload.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    {
                        messageId = id.GetString();
                    }
                }
                return messageId == null || IsOtherMessageId(messageId);
            default:
                return false;
        }
    }

    private static void ValidateReplyChainId(string value, string parameterName)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value is "." or "..")
        {
            throw ErrorHelper.CreateArgumentException(ErrorHelper.PlanInvalidReplyChainId, parameterName);
        }
    }

    private async Task SendUpdateAsync(PlanType operation, IEnumerable<PlanStep>? steps, CancellationToken cancellationToken)
    {
        var activity = new Activity
        {
            Type = ActivityTypes.Typing,
            Entities = [new PlanInfoEntity { PlanType = operation, Steps = steps?.ToList() }]
        }.Clone();
        var response = await PostAsync(activity, completion: false, cancellationToken).ConfigureAwait(false);
        if (response != null)
        {
            Sent?.Invoke(this, new TeamsPlanSentEventArgs(null));
        }
    }

    private static IActivity PrepareCompletion(IActivity message)
    {
        AssertionHelpers.ThrowIfNull(message, nameof(message));
        if (message.Type != ActivityTypes.Message)
        {
            throw ErrorHelper.CreateArgumentException(ErrorHelper.PlanCompletionMustBeMessage, nameof(message));
        }

        // Cloning materializes untyped entities, so validate targeting on the clone.
        var activity = message.Clone();
        if (activity.IsTargetedActivity())
        {
            throw ErrorHelper.CreateArgumentException(ErrorHelper.PlanTargetedCompletionNotSupported, nameof(message));
        }

        var plans = activity.Entities?.Where(entity => entity.Type == PlanInfoEntity.EntityName).ToArray() ?? [];
        if (plans.Any(entity => ProtocolJsonSerializer.ToObject<PlanInfoEntity>(entity).PlanType.Value != "complete"))
        {
            throw ErrorHelper.CreateArgumentException(ErrorHelper.PlanCompletionOperationInvalid, nameof(message));
        }
        if (plans.Length == 0)
        {
            activity.Entities ??= [];
            activity.Entities.Add(new PlanInfoEntity { PlanType = PlanTypes.Complete });
        }

        return activity;
    }

    private ConversationReference GetConversationReference()
    {
        var reference = ProtocolJsonSerializer.ToObject<ConversationReference>(_turnContext.Activity.GetConversationReference());
        reference.Conversation.Id = Options.ConversationId;
        reference.ActivityId = Options.ReplyToId;
        return reference;
    }

    private async Task<ResourceResponse> PostAsync(IActivity activity, bool completion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        activity.Id = null;
        Func<IActivity, CancellationToken, Task<ResourceResponse>>? sendReplyChain = null;
        if (completion && Options.ReplyChain is { } replyChain)
        {
            sendReplyChain = (outgoing, token) =>
                SendReplyChainAsync(replyChain, outgoing, token);
        }
        var responses = await _turnContext.SendPlanActivityAsync(
            activity,
            GetConversationReference(),
            sendReplyChain,
            cancellationToken).ConfigureAwait(false);
        return responses?.FirstOrDefault();
    }

    /// <summary>
    /// Posts a fresh Plan completion into the saved Teams chat reply chain.
    /// </summary>
    /// <remarks>
    /// Used by the Teams-owned outgoing send hook, not as a replacement for ordinary adapter delivery.
    /// Hosted responses and established streams remain on the adapter path.
    /// <see cref="Microsoft.Agents.Connector.IConversations"/> has no reply-chain operation, so this
    /// method uses the current turn's <see cref="Microsoft.Agents.Connector.IRestTransport"/> for its
    /// service-scoped endpoint and configured HTTP client.
    /// </remarks>
    private async Task<ResourceResponse> SendReplyChainAsync(
        PlanReplyChain replyChain,
        IActivity activity,
        CancellationToken cancellationToken)
    {
        var connector = ((ITurnContext)_turnContext).Services.Get<IConnectorClient>()
            ?? throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.PlanConnectorClientMissing, null);
        if (connector is not IRestTransport transport || transport.Endpoint == null)
        {
            throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.PlanReplyChainTransportRequired, null);
        }

        // Saved chat/root IDs select the thread; the live turn supplies the service URL and credentials.
        var uri = new Uri(
            $"{transport.Endpoint.AbsoluteUri.TrimEnd('/')}/v3/conversations/{Uri.EscapeDataString(replyChain.ChatOrChannelId)}/replyChains/{Uri.EscapeDataString(replyChain.ReplyChainId)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(ProtocolJsonSerializer.ToJson(activity), Encoding.UTF8, "application/json")
        };
        using var client = await transport.GetHttpClientAsync().ConfigureAwait(false);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.Accepted)
        {
            return string.IsNullOrEmpty(content)
                ? new ResourceResponse { Id = string.Empty }
                : ProtocolJsonSerializer.ToObject<ResourceResponse>(content);
        }

        // Keep the channel status and error code for conflict/timeout handling; retry decisions belong to the caller.
        var error = ErrorResponseException.CreateErrorResponseException(
            ErrorHelper.PlanReplyChainRequestFailed, null,
            ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture), response.ReasonPhrase ?? string.Empty);
        error.StatusCode = (int)response.StatusCode;
        try
        {
            if (!string.IsNullOrEmpty(content))
            {
                error.Body = ProtocolJsonSerializer.ToObject<ErrorResponse>(content);
            }
        }
        catch (JsonException)
        {
            // An unrecognized error body must not hide the HTTP failure.
        }

        throw error;
    }
}

/// <summary>
/// A successful Plan send, with a Core response only when a final message was created.
/// </summary>
[Experimental("ExperimentalTeamsPlan")]
public sealed class TeamsPlanSentEventArgs(ResourceResponse? response) : EventArgs
{
    /// <summary>
    /// Gets the final message response, or null for a typing acknowledgement.
    /// </summary>
    public ResourceResponse? Response { get; } = response;
}
