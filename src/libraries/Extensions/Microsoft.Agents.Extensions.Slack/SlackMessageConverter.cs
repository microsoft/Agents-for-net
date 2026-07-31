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
    private readonly SlackAttachmentConverter _attachmentConverter;

    internal SlackMessageConverter(SlackAttachmentConverter attachmentConverter)
    {
        _attachmentConverter = attachmentConverter
            ?? throw new System.ArgumentNullException(nameof(attachmentConverter));
    }

    internal async Task<IReadOnlyList<SlackMessagePayload>> ConvertAsync(
        IActivity activity,
        string channel,
        string? threadTs,
        string token,
        CancellationToken cancellationToken)
    {
        if (!activity.IsType(ActivityTypes.Message))
        {
            return [];
        }

        List<SlackMessagePayload> payloads = [];
        activity.TryGetChannelData<SlackChannelData>(out var slackChannelData);
        var convertedAttachments = await _attachmentConverter.ConvertAsync(
            activity.Attachments,
            activity.From?.Id ?? string.Empty,
            channel,
            token,
            slackChannelData?.RenderButtonsAsMenu == true,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(activity.Text) || convertedAttachments.Count > 0)
        {
            payloads.Add(new SlackMessagePayload
            {
                Channel = channel,
                Text = string.IsNullOrEmpty(activity.Text)
                    ? null
                    : activity.Text.SlackEncode(),
                ThreadTs = threadTs,
                Attachments = convertedAttachments.Count > 0
                    ? convertedAttachments
                    : null,
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

        return payloads;
    }
}
