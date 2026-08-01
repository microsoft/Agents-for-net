// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable

using Microsoft.Agents.Extensions.Slack.Api;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack;

internal interface ISlackFileUploader
{
    Task<string?> UploadAsync(
        byte[] content,
        string fileName,
        string channel,
        string token,
        CancellationToken cancellationToken);
}

internal sealed class SlackFileUploader : ISlackFileUploader
{
    private readonly SlackApi _slackApi;

    internal SlackFileUploader(SlackApi slackApi)
    {
        _slackApi = slackApi ?? throw new ArgumentNullException(nameof(slackApi));
    }

    public async Task<string?> UploadAsync(
        byte[] content,
        string fileName,
        string channel,
        string token,
        CancellationToken cancellationToken)
    {
        var uploadTarget = await _slackApi.CallAsync(
            "files.getUploadURLExternal",
            new
            {
                filename = fileName,
                length = content.Length,
            },
            token,
            cancellationToken).ConfigureAwait(false);

        var uploadUrl = uploadTarget.Get<string>("upload_url");
        var fileId = uploadTarget.Get<string>("file_id");
        if (string.IsNullOrWhiteSpace(uploadUrl) || string.IsNullOrWhiteSpace(fileId))
        {
            throw new SlackResponseException(
                "Slack API error on files.getUploadURLExternal: response did not include upload_url and file_id.");
        }

        await _slackApi.UploadContentAsync(uploadUrl, content, cancellationToken).ConfigureAwait(false);

        var completed = await _slackApi.CallAsync(
            "files.completeUploadExternal",
            new
            {
                files = new[]
                {
                    new
                    {
                        id = fileId,
                        title = fileName,
                    },
                },
                channel_id = channel,
            },
            token,
            cancellationToken).ConfigureAwait(false);

        return completed.Get<string>("files[0].url_private")
            ?? completed.Get<string>("files[0].permalink");
    }
}
