// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.App.MessageExtensions;

/// <summary>
/// Provides a builder for configuring message preview edit routes in an AgentApplication.
/// </summary>
public class MessagePreviewEditRouteBuilder : CommandRouteBuilderBase<MessagePreviewEditRouteBuilder>
{
    public MessagePreviewEditRouteBuilder() : base()
    {
        PreviewAction = Microsoft.Teams.Api.MessageExtensions.MessagePreviewAction.Edit.ToString();
        InvokeName = Microsoft.Teams.Api.Activities.Invokes.Name.MessageExtensions.SubmitAction;
    }

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

