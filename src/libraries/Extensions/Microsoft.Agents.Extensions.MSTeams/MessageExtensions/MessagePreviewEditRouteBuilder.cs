// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;
using System;

namespace Microsoft.Agents.Extensions.MSTeams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring message preview edit routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="MessagePreviewEditRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Apps.InvokeNames.MessageExtensionSubmitAction"/>
/// with <see cref="Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction.BotMessagePreviewAction"/> of <c>"edit"</c>,
/// optionally filtered by command ID via <see cref="WithCommand(string)"/>.
/// </remarks>
public class MessagePreviewEditRouteBuilder : CommandRouteBuilderBase<MessagePreviewEditRouteBuilder>
{
    /// <summary>
    /// Creates a new instance of the MessagePreviewEditRouteBuilder class for constructing route definitions.
    /// </summary>
    /// <returns>A MessagePreviewEditRouteBuilder instance that can be used to configure and build routes.</returns>
    public static MessagePreviewEditRouteBuilder Create()
    {
        var builder = Activator.CreateInstance<MessagePreviewEditRouteBuilder>();
        return builder;
    }

    public MessagePreviewEditRouteBuilder() : base()
    {
        PreviewAction = Microsoft.Teams.Apps.MessageExtensions.BotMessagePreviewActionTypes.Edit.ToString();
        InvokeName = Microsoft.Teams.Apps.InvokeNames.MessageExtensionSubmitAction;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing message preview edit actions.
    /// </summary>
    /// <param name="handler">An asynchronous delegate that processes the message preview edit action.</param>
    /// <returns>The current instance of <see cref="MessagePreviewEditRouteBuilder"/>, enabling method chaining.</returns>
    public MessagePreviewEditRouteBuilder WithHandler(MessagePreviewEditHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var messagingExtensionAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Apps.MessageExtensions.MessageExtensionAction>(ctx.Activity.Value);
            var response = await handler(new TeamsTurnContext(ctx), ts, messagingExtensionAction.BotActivityPreview?[0], ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }
}
