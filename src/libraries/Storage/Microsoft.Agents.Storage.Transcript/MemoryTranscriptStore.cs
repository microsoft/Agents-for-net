// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Agents.Storage.Transcript
{
    /// <summary>
    /// The memory transcript store stores transcripts in volatile memory in a Dictionary.
    /// </summary>
    /// <remarks>
    /// Because this uses an unbounded volatile dictionary this should only be used for unit tests or non-production environments.
    /// </remarks>
    public class MemoryTranscriptStore : ITranscriptStore
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ImmutableList<IActivity>>> _channels = new();

        /// <summary>
        /// Logs an activity to the transcript.
        /// </summary>
        /// <param name="activity">The activity to log.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public Task LogActivityAsync(IActivity activity)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity), "activity cannot be null for LogActivity()");
            }

            var channel = _channels.GetOrAdd(activity.ChannelId, _ => new ConcurrentDictionary<string, ImmutableList<IActivity>>());

            channel.AddOrUpdate(
                activity.Conversation.Id,
                // Add factory - create new list with the activity
                _ => ProcessActivity(ImmutableList<IActivity>.Empty, activity),
                // Update factory - add to existing list
                (_, existingTranscript) => ProcessActivity(existingTranscript, activity)
            );

            return Task.CompletedTask;
        }

        private static ImmutableList<IActivity> ProcessActivity(ImmutableList<IActivity> transcript, IActivity activity)
        {
            switch (activity.Type)
            {
                case ActivityTypes.MessageDelete:
                    // if message delete comes in, delete the message from the transcript
                    var deleteIndex = transcript.FindIndex(a => a.Id == activity.Id);
                    if (deleteIndex >= 0)
                    {
                        var originalActivity = transcript[deleteIndex];
                        var tombstone = new Activity()
                        {
                            Type = ActivityTypes.MessageDelete,
                            Id = originalActivity.Id,
                            From = new ChannelAccount(id: "deleted", role: originalActivity.From.Role),
                            Recipient = new ChannelAccount(id: "deleted", role: originalActivity.Recipient.Role),
                            Locale = originalActivity.Locale,
                            LocalTimestamp = originalActivity.Timestamp,
                            Timestamp = originalActivity.Timestamp,
                            ChannelId = originalActivity.ChannelId,
                            Conversation = originalActivity.Conversation,
                            ServiceUrl = originalActivity.ServiceUrl,
                            ReplyToId = originalActivity.ReplyToId,
                        };
                        return transcript.SetItem(deleteIndex, tombstone);
                    }
                    return transcript;

                case ActivityTypes.MessageUpdate:
                    var updateIndex = transcript.FindIndex(a => a.Id == activity.Id);
                    if (updateIndex >= 0)
                    {
                        var originalActivity = transcript[updateIndex];
                        var updatedActivity = activity.Clone();
                        updatedActivity.Type = originalActivity.Type; // fixup original type (should be Message)
                        updatedActivity.LocalTimestamp = originalActivity.LocalTimestamp;
                        updatedActivity.Timestamp = originalActivity.Timestamp;
                        return transcript.SetItem(updateIndex, updatedActivity);
                    }
                    return transcript;

                default:
                    return transcript.Add(activity);
            }
        }

        /// <summary>
        /// Gets from the store activities that match a set of criteria.
        /// </summary>
        /// <param name="channelId">The ID of the channel the conversation is in.</param>
        /// <param name="conversationId">The ID of the conversation.</param>
        /// <param name="continuationToken">The continuation token from the previous page of results.</param>
        /// <param name="startDate">A cutoff date. Activities older than this date are not included.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task completes successfully, the result contains a page of matching activities.</remarks>
        public Task<PagedResult<IActivity>> GetTranscriptActivitiesAsync(string channelId, string conversationId, string continuationToken = null, DateTimeOffset startDate = default)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));

            var pagedResult = new PagedResult<IActivity>();

            if (_channels.TryGetValue(channelId, out var channel))
            {
                if (channel.TryGetValue(conversationId, out var transcript))
                {
                    if (continuationToken != null)
                    {
                        pagedResult.Items = transcript
                            .OrderBy(a => a.Timestamp)
                            .Where(a => a.Timestamp >= startDate)
                            .SkipWhile(a => a.Id != continuationToken)
                            .Skip(1)
                            .Take(20)
                            .ToArray();

                        if (pagedResult.Items.Count == 20)
                        {
                            pagedResult.ContinuationToken = pagedResult.Items.Last().Id;
                        }
                    }
                    else
                    {
                        pagedResult.Items = transcript
                            .OrderBy(a => a.Timestamp)
                            .Where(a => a.Timestamp >= startDate)
                            .Take(20)
                            .ToArray();

                        if (pagedResult.Items.Count == 20)
                        {
                            pagedResult.ContinuationToken = pagedResult.Items.Last().Id;
                        }
                    }
                }
            }

            return Task.FromResult(pagedResult);
        }

        /// <summary>
        /// Deletes conversation data from the store.
        /// </summary>
        /// <param name="channelId">The ID of the channel the conversation is in.</param>
        /// <param name="conversationId">The ID of the conversation to delete.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        public Task DeleteTranscriptAsync(string channelId, string conversationId)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));
            AssertionHelpers.ThrowIfNullOrWhiteSpace(conversationId, nameof(conversationId));

            if (_channels.TryGetValue(channelId, out var channel))
            {
                channel.TryRemove(conversationId, out _);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets a page of conversations for a channel from the store.
        /// </summary>
        /// <param name="channelId">The ID of the channel.</param>
        /// <param name="continuationToken">The continuation token from the previous page of results.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        /// <remarks>If the task is successful, the result contains a page of conversations.</remarks>
        public Task<PagedResult<TranscriptInfo>> ListTranscriptsAsync(string channelId, string continuationToken = null)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(channelId, nameof(channelId));

            var pagedResult = new PagedResult<TranscriptInfo>();

            if (_channels.TryGetValue(channelId, out var channel))
            {
                // Take a snapshot of the channel for consistent iteration
                var channelSnapshot = channel.ToArray();

                if (continuationToken != null)
                {
                    pagedResult.Items = channelSnapshot.Select(c => new TranscriptInfo()
                    {
                        ChannelId = channelId,
                        Id = c.Key,
                        Created = c.Value.FirstOrDefault()?.Timestamp ?? default,
                    })
                    .OrderBy(c => c.Created)
                    .SkipWhile(c => c.Id != continuationToken)
                    .Skip(1)
                    .Take(20)
                    .ToArray();

                    if (pagedResult.Items.Count == 20)
                    {
                        pagedResult.ContinuationToken = pagedResult.Items.Last().Id;
                    }
                }
                else
                {
                    pagedResult.Items = channelSnapshot.Select(
                        c => new TranscriptInfo
                        {
                            ChannelId = channelId,
                            Id = c.Key,
                            Created = c.Value.FirstOrDefault()?.Timestamp ?? default,
                        })
                        .OrderBy(c => c.Created)
                        .Take(20)
                        .ToArray();

                    if (pagedResult.Items.Count == 20)
                    {
                        pagedResult.ContinuationToken = pagedResult.Items.Last().Id;
                    }
                }
            }

            return Task.FromResult(pagedResult);
        }
    }
}
