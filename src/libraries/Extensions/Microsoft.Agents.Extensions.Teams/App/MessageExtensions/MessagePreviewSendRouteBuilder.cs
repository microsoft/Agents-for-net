// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Provides a builder for configuring message preview send routes in an AgentApplication.
/// </summary>
public class MessagePreviewSendRouteBuilder : CommandRouteBuilderBase<MessagePreviewSendRouteBuilder>
{
    public MessagePreviewSendRouteBuilder() : base()
    {
        PreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Send.ToString();
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction;
    }

    public MessagePreviewSendRouteBuilder WithHandler(AgentMessagePreviewSendHandler handler)
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
