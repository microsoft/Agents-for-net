// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.FileConsents;

/// <summary>
/// Provides a builder for configuring routes that handle Teams file consent accept invocations.
/// </summary>
/// <remarks>
/// Use <see cref="FileConsentAcceptRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.FileConsent"/>
/// and <see cref="Microsoft.Teams.Api.FileConsentCardResponse.Action"/> of <c>"accept"</c>.
/// </remarks>
public class FileConsentAcceptRouteBuilder : FileConsentRouteBuilderBase<FileConsentAcceptRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="FileConsentAcceptRouteBuilder"/>,
    /// pre-configured to match file consent accept invocations.
    /// </summary>
    public FileConsentAcceptRouteBuilder() : base()
    {
        Action = Microsoft.Teams.Api.Action.Accept;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing file consent accept invocations.
    /// </summary>
    /// <param name="handler">An asynchronous delegate invoked when the user accepts the file consent card.
    /// Receives the turn context, turn state, deserialized <see cref="Microsoft.Teams.Api.FileConsentCardResponse"/>,
    /// and a cancellation token.</param>
    /// <param name="proactive">The proactive messaging helper used to create a <see cref="TeamsTurnContext"/> for the handler.</param>
    /// <returns>The current <see cref="FileConsentAcceptRouteBuilder"/> instance for method chaining.</returns>
    public FileConsentAcceptRouteBuilder WithHandler(FileConsentHandler handler, Microsoft.Agents.Builder.App.Proactive.Proactive proactive)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var ttc = new TeamsTurnContext(ctx, proactive);
            var response = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.FileConsentCardResponse>(ctx.Activity.Value);
            await handler(ttc, ts, response, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ttc).ConfigureAwait(false);
        };
        return this;
    }
}
