// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.MessageExtensions;

/// <summary>
/// Provides a builder for configuring message preview edit routes in an AgentApplication.
/// </summary>
/// <remarks>
/// Use <see cref="MessagePreviewEditRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction"/>
/// with <see cref="Microsoft.Teams.Api.MessageExtensions.Action.BotMessagePreviewAction"/> of <c>"edit"</c>,
/// optionally filtered by command ID via <see cref="WithCommand(string)"/>.
/// <code>
/// var route = MessagePreviewEditRouteBuilder.Create()
///     .WithCommand("actionCmd")
///     .WithHandler(async (context, state, activity, ct) =>
///     {
///         // Handle edit of the message preview
///     })
///     .Build();
///
/// app.AddRoute(route);
/// </code>
/// </remarks>
public class MessagePreviewEditRouteBuilder : CommandRouteBuilderBase<MessagePreviewEditRouteBuilder>
{
    public MessagePreviewEditRouteBuilder() : base()
    {
        PreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Edit.ToString();
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction;
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
            var messagingExtensionAction = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.MessageExtensions.Action>(ctx.Activity.Value);
            var response = await handler(ctx, ts, messagingExtensionAction.BotActivityPreview[0].ToCoreActivity(), ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx, response).ConfigureAwait(false);
        };
        return this;
    }
}

