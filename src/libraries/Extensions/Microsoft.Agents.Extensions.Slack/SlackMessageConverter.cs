// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Extensions.Slack.Api;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack;

internal sealed class SlackMessageConverter
{
    internal Task<IReadOnlyList<SlackMessagePayload>> ConvertAsync(
        IActivity activity,
        string channel,
        string? threadTs,
        string token,
        CancellationToken cancellationToken)
    {
        if (!activity.IsType(ActivityTypes.Message))
        {
            return Task.FromResult<IReadOnlyList<SlackMessagePayload>>([]);
        }

        List<SlackMessagePayload> payloads = [];

        if (!string.IsNullOrEmpty(activity.Text))
        {
            payloads.Add(new SlackMessagePayload
            {
                Channel = channel,
                Text = activity.Text.SlackEncode(),
                ThreadTs = threadTs,
            });
        }

        if (activity.SuggestedActions?.Actions?.Count > 0)
        {
            var lines = activity.SuggestedActions.Actions.Select(action =>
            {
                var value = action.Type == ActionTypes.MessageBack
                    ? action.Text
                    : action.Value as string;
                return $"* {value ?? action.Title}";
            });

            payloads.Add(new SlackMessagePayload
            {
                Channel = channel,
                Text = string.Join("\n\n", lines),
                ThreadTs = threadTs,
            });
        }

        return Task.FromResult<IReadOnlyList<SlackMessagePayload>>(payloads);
    }
}
