// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;
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
        // https://docs.slack.dev/reference/methods/chat.startStream
        var result = await _slackApi.CallAsync("chat.startStream", new
        {
            channel = _channel,
            thread_ts = _threadTs,
            task_display_mode = taskDisplayMode,
        }, _token);

        _messageTs = result.ts;
        return this;
    }

    public Task<SlackStream> AppendAsync(string markdown_text)
    {
        return AppendAsync(new MarkdownTextChunk(markdown_text ?? ""));
    }

    public Task<SlackStream> AppendAsync(Chunk chunk)
    {
        AssertionHelpers.ThrowIfNull(chunk, nameof(chunk));
        return AppendAsync([chunk]);
    }

    public async Task<SlackStream> AppendAsync(IList<Chunk> chunks)
    {
        AssertionHelpers.ThrowIfNull(chunks, nameof(chunks));

        // https://docs.slack.dev/reference/methods/chat.appendStream
        await _slackApi.CallAsync("chat.appendStream", new
        {
            channel = _channel,
            ts = _messageTs,
            thread_ts = _threadTs,
            chunks,
        }, _token);

        return this;
    }

    public async Task StopAsync(IList<Chunk>? chunks = null, IList<object> blocks = null)
    {
        if (string.IsNullOrEmpty(_messageTs))
        {
            return;
        }

        // https://docs.slack.dev/reference/methods/chat.stopStream
        await _slackApi.CallAsync("chat.stopStream", new
        {
            channel = _channel,
            ts = _messageTs,
            thread_ts = _threadTs,
            chunks,
            blocks,
        }, _token);
    }

    public async Task StopAsync(IList<Chunk>? chunks = null, string blocks = null)
    {
        if (string.IsNullOrEmpty(_messageTs))
        {
            return;
        }

        var jsonElement = JsonSerializer.Deserialize<JsonElement>(blocks);
        if (jsonElement.ValueKind == JsonValueKind.Object)
        {
            if (jsonElement.TryGetProperty("blocks", out JsonElement value))
            {
                jsonElement = value;
            }
            else
            {
                throw new ArgumentException("If blocks is a JSON object, it must contain a \"blocks\" property with a JSON array value.", nameof(blocks));
            }
        }
        else if (jsonElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("Blocks must be a JSON array or an object containing a \"blocks\" property with a JSON array value.", nameof(blocks));
        }

        // https://docs.slack.dev/reference/methods/chat.stopStream
        await _slackApi.CallAsync("chat.stopStream", new
        {
            channel = _channel,
            ts = _messageTs,
            thread_ts = _threadTs,
            chunks,
            blocks = jsonElement,
        }, _token);
    }
}