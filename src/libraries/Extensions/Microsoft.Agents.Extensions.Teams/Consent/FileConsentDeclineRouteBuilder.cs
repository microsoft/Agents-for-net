// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Extensions.Teams.Consent;

/// <summary>
/// Provides a builder for configuring routes that handle Teams file consent decline invocations.
/// </summary>
/// <remarks>
/// Use <see cref="FileConsentDeclineRouteBuilder"/> to create and configure routes that respond to Activity Type of
/// <see cref="Microsoft.Agents.Core.Models.ActivityTypes.Invoke"/> with a name of
/// <see cref="Microsoft.Teams.Api.Activities.Invokes.Name.FileConsent"/>
/// and <see cref="Microsoft.Teams.Api.FileConsentCardResponse.Action"/> of <c>"decline"</c>.
/// </remarks>
public class FileConsentDeclineRouteBuilder : FileConsentRouteBuilderBase<FileConsentDeclineRouteBuilder>
{
    /// <summary>
    /// Initializes a new instance of <see cref="FileConsentDeclineRouteBuilder"/>,
    /// pre-configured to match file consent decline invocations.
    /// </summary>
    public FileConsentDeclineRouteBuilder() : base()
    {
        Action = Microsoft.Teams.Api.Action.Decline;
    }

    /// <summary>
    /// Configures the route to use the specified handler for processing file consent decline invocations.
    /// </summary>
    /// <param name="handler">An asynchronous delegate invoked when the user declines the file consent card.
    /// Receives the turn context, turn state, deserialized <see cref="Microsoft.Teams.Api.FileConsentCardResponse"/>,
    /// and a cancellation token.</param>
    /// <returns>The current <see cref="FileConsentDeclineRouteBuilder"/> instance for method chaining.</returns>
    public FileConsentDeclineRouteBuilder WithHandler(FileConsentHandler handler)
    {
        _route.Handler = async (ctx, ts, ct) =>
        {
            var response = ProtocolJsonSerializer.ToObject<Microsoft.Teams.Api.FileConsentCardResponse>(ctx.Activity.Value);
            await handler(ctx, ts, response, ct).ConfigureAwait(false);
            await TeamsAgentExtension.SetResponse(ctx).ConfigureAwait(false);
        };
        return this;
    }
}
