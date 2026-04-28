// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.Slack.Api;

/// <summary>
/// Provides an interface for streaming message content to a Slack channel or thread using the Slack API. Supports
/// starting, appending to, and stopping a message stream with support for rich content blocks and markdown.
/// </summary>
/// <remarks>A SlackStream instance is typically used to incrementally build and update a Slack message in real
/// time, such as for long-running tasks or interactive workflows. The stream must be started with StartAsync before
/// appending content, and stopped with StopAsync when complete. This class is not thread-safe; concurrent operations on
/// the same instance may result in undefined behavior.</remarks>
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

    /// <summary>
    /// Starts a new Slack message stream asynchronously using the specified task display mode. 
    /// </summary>
    /// <remarks>See https://docs.slack.dev/reference/methods/chat.startStream</remarks>
    /// <param name="taskDisplayMode">The display mode for the task in the Slack stream. The default is TaskDisplayMode.Plan.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the current SlackStream instance.</returns>
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

    /// <summary>
    /// Appends a new chunk of Markdown-formatted text to the stream asynchronously.
    /// </summary>
    /// <param name="markdown_text">The Markdown-formatted text to append. If null, an empty string is used.</param>
    /// <returns>A task that represents the asynchronous append operation. The task result contains the updated Slack stream.</returns>
    public Task<SlackStream> AppendAsync(string markdown_text)
    {
        return AppendAsync(new MarkdownTextChunk(markdown_text ?? ""));
    }

    /// <summary>
    /// Appends the specified chunk to the stream asynchronously.
    /// </summary>
    /// <param name="chunk">The chunk to append to the stream. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous append operation. The task result contains the updated stream after the
    /// chunk is appended.</returns>
    public Task<SlackStream> AppendAsync(Chunk chunk)
    {
        AssertionHelpers.ThrowIfNull(chunk, nameof(chunk));
        return AppendAsync([chunk]);
    }

    /// <summary>
    /// Appends the specified chunks to the Slack message stream asynchronously.
    /// </summary>
    /// <remarks>See https://docs.slack.dev/reference/methods/chat.appendStream</remarks>
    /// <param name="chunks">The list of chunks to append to the message stream. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the current Slack stream instance.</returns>
    public async Task<SlackStream> AppendAsync(IList<Chunk> chunks)
    {
        AssertionHelpers.ThrowIfNull(chunks, nameof(chunks));

        if (chunks.Count == 0)
        {
            return this;
        }

        await _slackApi.CallAsync("chat.appendStream", new
        {
            channel = _channel,
            ts = _messageTs,
            thread_ts = _threadTs,
            chunks,
        }, _token);

        return this;
    }

    /// <summary>
    /// Stops the active Slack message stream for the specified channel and message.
    /// </summary>
    /// <remarks>See https://docs.slack.dev/reference/methods/chat.stopStream</remarks>
    /// <param name="chunks">An optional list of message chunks to include in the stop request. May be null to omit chunks.</param>
    /// <param name="blocks">An optional list of block elements to include in the stop request. May be null to omit blocks.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    public async Task StopAsync(IList<Chunk>? chunks = null, IList<object>? blocks = null)
    {
        if (string.IsNullOrEmpty(_messageTs))
        {
            return;
        }

        await _slackApi.CallAsync("chat.stopStream", new
        {
            channel = _channel,
            ts = _messageTs,
            thread_ts = _threadTs,
            chunks,
            blocks,
        }, _token);
    }

    /// <summary>
    /// Stops the active Slack message stream for the current channel and message, optionally updating the message with
    /// the specified chunks and blocks.
    /// </summary>
    /// <remarks>This method calls the Slack API method "chat.stopStream" to end the message stream. The
    /// blocks parameter must conform to Slack's block kit structure.<br/><br/>
    /// See https://docs.slack.dev/reference/methods/chat.stopStream
    /// </remarks>
    /// <param name="chunks">An optional list of chunks to include in the final message update. If null, no chunks are sent.</param>
    /// <param name="blocks">An optional JSON string representing the message blocks to update. Must be a JSON array or an object containing
    /// a "blocks" property with a JSON array value. If null, no blocks are updated.</param>
    /// <returns>A task that represents the asynchronous stop operation.</returns>
    /// <exception cref="ArgumentException">Thrown if blocks is a JSON object that does not contain a "blocks" property with a JSON array value, or if
    /// blocks is not a valid JSON array or object.</exception>
    public async Task StopAsync(IList<Chunk>? chunks = null, string? blocks = null)
    {
        if (string.IsNullOrEmpty(_messageTs))
        {
            return;
        }

        if (blocks == null)
        {
            await StopAsync(chunks, (IList<object>?)null);
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