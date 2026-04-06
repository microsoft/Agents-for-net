// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring message preview send routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="MessagePreviewSendRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction"/>
/// with <see cref="Microsoft.Teams.Api.MessageExtensions.Action.BotMessagePreviewAction"/> of <c>"send"</c>,
/// optionally filtered by command ID via <see cref="WithCommand(string)"/>.
/// </remarks>
public class MessagePreviewSendRouteBuilder : CommandRouteBuilderBase<MessagePreviewSendRouteBuilder>
{
    public MessagePreviewSendRouteBuilder() : base()
    {
        PreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Send.ToString();
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing message preview send actions.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the message preview send action.</param>
    /// <returns>The current instance of <see cref="MessagePreviewSendRouteBuilder"/>, enabling method chaining.</returns>
    public MessagePreviewSendRouteBuilder WithHandler(MessagePreviewSendHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var messagingExtensionAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(ctx.Activity.Value);
            await handler(ctx, ts, messagingExtensionAction.BotActivityPreview[0].ToCoreActivity(), ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, new Microsoft.Teams.Api.MessageExtensions.Response()).ConfigureAwait(false);
        };
        return this;
    }
}
