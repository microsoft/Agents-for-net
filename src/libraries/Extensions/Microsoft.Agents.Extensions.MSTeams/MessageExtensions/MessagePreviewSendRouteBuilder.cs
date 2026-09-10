// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;

namespace Microsoft.Agents.Extensions.MSTeams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring message preview send routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="MessagePreviewSendRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Apps.InvokeNames.MessageExtensionSubmitAction"/>
/// with <see cref="Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction.BotMessagePreviewAction"/> of <c>"send"</c>,
/// optionally filtered by command ID via <see cref="WithCommand(string)"/>.
/// </remarks>
public class MessagePreviewSendRouteBuilder : CommandRouteBuilderBase<MessagePreviewSendRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the MessagePreviewSendRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A MessagePreviewSendRouteBuilder instance that can be used to configure and build routes.</returns>
    public static MessagePreviewSendRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<MessagePreviewSendRouteBuilder>();
        return builder;
    }

    public MessagePreviewSendRouteBuilder() : base()
    {
        PreviewAction = Microsoft.Teams.Apps.MessageExtensions.BotMessagePreviewActionTypes.Send.ToString();
        InvokeName = Microsoft.Teams.Apps.InvokeNames.MessageExtensionSubmitAction;
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
            var messagingExtensionAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction>(ctx.Activity.Value);
            await handler(new TeamsTurnContext(ctx), ts, messagingExtensionAction.BotActivityPreview?[0], ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, new Microsoft.Teams.Apps.MessageExtensions.MessageExtensionResponse()).ConfigureAwait(false);
        };
        return this;
    }
}
