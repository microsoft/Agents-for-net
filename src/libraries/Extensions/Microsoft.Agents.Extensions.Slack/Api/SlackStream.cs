// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api;

public class SlackStream
{
    private string? _messageTs;
    private readonly string _channel;
    private readonly string _threadTs;
    private readonly string _token;
    private readonly SlackApi _slackApi;

    internal SlackStream(SlackApi slackApi, string channel, string threadTs, string token)
    {
        _channel = channel;
        _threadTs = threadTs;
        _token = token;
        _slackApi = slackApi;
    }

    public async Task<SlackStream> StartAsync(string taskDisplayMode = TaskDisplayMode.Plan)
    {
        var result = await _slackApi.CallAsync("chat.startStream", new
        {
            channel = _channel,
            thread_ts = _threadTs,
            task_display_mode = taskDisplayMode,
        }, _token);

        _messageTs = result.ts;
        return this;
    }

    public async Task<SlackStream> AppendAsync(List<Chunk> chunks)
    {
        await _slackApi.CallAsync("chat.appendStream", new
        {
            channel = _channel,
            ts = _messageTs,
            thread_ts = _threadTs,
            chunks,
        }, _token);
        return this;
    }

    public async Task StopAsync(List<Chunk>? chunks = null)
    {
        await _slackApi.CallAsync("chat.stopStream", new
        {
            channel = _channel,
            ts = _messageTs,
            thread_ts = _threadTs,
            chunks = chunks ?? [],
        }, _token);
    }
}
