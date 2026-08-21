// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.Slack.Api;

namespace Microsoft.Agents.Extensions.Slack;

/// <summary>
/// A Slack-aware <see cref="ITurnContext"/> that exposes Slack-specific helpers such as the Slack API client
/// and strongly-typed channel data on top of the underlying turn context.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SlackTurnContext"/> class wrapping the specified inner turn context.
/// </remarks>
/// <param name="turnContext">The inner turn context to wrap.</param>
public class SlackTurnContext(ITurnContext turnContext) : TurnContextWrapper(turnContext), ISlackTurnContext, ITurnContext<ISlackActivity>
{
    /// <inheritdoc/>
    public SlackApi Client => _turnContext.Services.Get<SlackApi>();

    /// <inheritdoc/>
    public new ISlackActivity Activity =>
        _turnContext.Activity as ISlackActivity ?? ProtocolJsonSerializer.ToObject<SlackActivity>(_turnContext.Activity);
}
