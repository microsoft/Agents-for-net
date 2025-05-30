﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;

namespace Microsoft.Agents.Storage.Transcript
{
    /// <summary>
    /// The blobs transcript store stores transcripts in an Azure Blob container.
    /// </summary>
    /// <remarks>
    /// Each activity is stored as json blob in structure of
    /// container/{channelId]/{conversationId}/{Timestamp.ticks}-{activity.id}.json.
    /// </remarks>
    public class BlobsTranscriptStore : ITranscriptStore
    {
        // Containers checked for creation.
        private static readonly HashSet<string> _checkedContainers = [];

        // If a JsonSerializer is not provided during construction, this will be the default static JsonSerializer.
        private readonly JsonSerializerOptions _serializerOptions;

        private readonly Lazy<BlobContainerClient> _containerClient;

        private readonly StorageTransferOptions _storageTransferOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobsTranscriptStore"/> class.
        /// </summary>
        /// <param name="dataConnectionString">Azure Storage connection string.</param>
        /// <param name="containerName">Name of the Blob container where entities will be stored.</param>
        /// <param name="jsonSerializer">If passing in a custom JsonSerializerOptions.</param>
        public BlobsTranscriptStore(string dataConnectionString, string containerName, JsonSerializerOptions jsonSerializer = null)
            : this(dataConnectionString, containerName, default, jsonSerializer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobsTranscriptStore"/> class.
        /// </summary>
        /// <param name="blobServiceUri">A Uri referencing the blob container that includes the name of the account and the name of the container.</param>
        /// <param name="tokenCredential">The token credential to authenticate to the Azure storage.</param>
        /// <param name="containerName">Name of the Blob container where entities will be stored.</param>
        /// <param name="jsonSerializer">If passing in a custom JsonSerializerOptions.</param>
        public BlobsTranscriptStore(Uri blobServiceUri, TokenCredential tokenCredential, string containerName, JsonSerializerOptions jsonSerializer = null)
            : this(blobServiceUri, tokenCredential, containerName, default, jsonSerializer)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobsTranscriptStore"/> class.
        /// </summary>
        /// <param name="dataConnectionString">Azure Storage connection string.</param>
        /// <param name="containerName">Name of the Blob container where entities will be stored.</param>
        /// <param name="storageTransferOptions">Used for providing options for parallel transfers <see cref="StorageTransferOptions"/>.</param>
        /// <param name="jsonSerializer">If passing in a custom JsonSerializerOptions.</param>
        public BlobsTranscriptStore(string dataConnectionString, string containerName, StorageTransferOptions storageTransferOptions, JsonSerializerOptions jsonSerializer = null)
        {
            if (string.IsNullOrEmpty(dataConnectionString))
            {
                throw new ArgumentNullException(nameof(dataConnectionString));
            }

            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            _storageTransferOptions = storageTransferOptions;

            _serializerOptions = jsonSerializer ?? ProtocolJsonSerializer.SerializationOptions;

            // Triggers a check for the existence of the container
            _containerClient = new Lazy<BlobContainerClient>(
                () =>
                {
                    containerName = containerName.ToLowerInvariant();
                    var containerClient = new BlobContainerClient(dataConnectionString, containerName);
                    if (!_checkedContainers.Contains(containerName))
                    {
                        containerClient.CreateIfNotExistsAsync().Wait();
                        _checkedContainers.Add(containerName);
                    }

                    return containerClient;
                }, isThreadSafe: true);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobsTranscriptStore"/> class.
        /// </summary>
        /// <param name="blobServiceUri">A Uri referencing the blob container that includes the name of the account and the name of the container.</param>
        /// <param name="tokenCredential">The token credential to authenticate to the Azure storage.</param>
        /// <param name="containerName">Name of the Blob container where entities will be stored.</param>
        /// <param name="storageTransferOptions">Used for providing options for parallel transfers <see cref="StorageTransferOptions"/>.</param>
        /// <param name="jsonSerializer">If passing in a custom JsonSerializerOptions.</param>
        public BlobsTranscriptStore(Uri blobServiceUri, TokenCredential tokenCredential, string containerName, StorageTransferOptions storageTransferOptions, JsonSerializerOptions jsonSerializer = null)
        {
            AssertionHelpers.ThrowIfNull(blobServiceUri, nameof(blobServiceUri));
            AssertionHelpers.ThrowIfNull(tokenCredential, nameof(tokenCredential));

            if (string.IsNullOrEmpty(containerName))
            {
                throw new ArgumentNullException(nameof(containerName));
            }

            _storageTransferOptions = storageTransferOptions;

            _serializerOptions = jsonSerializer ?? ProtocolJsonSerializer.SerializationOptions;

            // Triggers a check for the existence of the container
            _containerClient = new Lazy<BlobContainerClient>(
                () =>
                {
                    var containerClient = new BlobContainerClient(blobServiceUri, tokenCredential);
                    if (!_checkedContainers.Contains(containerName))
                    {
                        containerClient.CreateIfNotExistsAsync().Wait();
                        _checkedContainers.Add(containerName);
                    }

                    return containerClient;
                }, isThreadSafe: true);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlobsTranscriptStore"/> class.
        /// </summary>
        /// <param name="containerClient">The custom implementation of BlobContainerClient.</param>
        /// <param name="jsonSerializer">If passing in a custom JsonSerializerOptions.</param>
        internal BlobsTranscriptStore(BlobContainerClient containerClient, JsonSerializerOptions jsonSerializer = null)
        {
            _containerClient = new Lazy<BlobContainerClient>(() => containerClient);
            _serializerOptions = jsonSerializer ?? ProtocolJsonSerializer.SerializationOptions;
        }

        /// <summary>
        /// Log an activity to the transcript.
        /// </summary>
        /// <param name="activity">Activity being logged.</param>
        /// <returns>A <see cref="Task"/>A task that represents the work queued to execute.</returns>
        public async Task LogActivityAsync(IActivity activity)
        {
            AssertionHelpers.ThrowIfNull(activity, nameof(activity));

            switch (activity.Type)
            {
                case ActivityTypes.MessageUpdate:
                    {
                        var updatedActivity = activity.Clone();
                        updatedActivity.Type = ActivityTypes.Message; // fixup original type (should be Message)

                        var activityAndBlob = await InnerReadBlobAsync(activity).ConfigureAwait(false);
                        if (activityAndBlob != default && activityAndBlob.Item1 != null)
                        {
                            updatedActivity.LocalTimestamp = activityAndBlob.Item1.LocalTimestamp;
                            updatedActivity.Timestamp = activityAndBlob.Item1.Timestamp;
                            await LogActivityToBlobClientAsync(updatedActivity, activityAndBlob.Item2, true).ConfigureAwait(false);
                        }
                        else
                        {
                            // The activity was not found, so just add a record of this update.
                            await InnerLogActivityAsync(updatedActivity, false).ConfigureAwait(false);
                        }

                        return;
                    }

                case ActivityTypes.MessageDelete:
                    {
                        var activityAndBlob = await InnerReadBlobAsync(activity).ConfigureAwait(false);
                        if (activityAndBlob != default && activityAndBlob.Item1 != null)
                        {
                            // tombstone the original message
                            var tombstonedActivity = new Activity()
                            {
                                Type = ActivityTypes.MessageDelete,
                                Id = activityAndBlob.Item1.Id,
                                From = new ChannelAccount(id: "deleted", role: activityAndBlob.Item1.From.Role),
                                Recipient = new ChannelAccount(id: "deleted", role: activityAndBlob.Item1.Recipient.Role),
                                Locale = activityAndBlob.Item1.Locale,
                                LocalTimestamp = activityAndBlob.Item1.Timestamp,
                                Timestamp = activityAndBlob.Item1.Timestamp,
                                ChannelId = activityAndBlob.Item1.ChannelId,
                                Conversation = activityAndBlob.Item1.Conversation,
                                ServiceUrl = activityAndBlob.Item1.ServiceUrl,
                                ReplyToId = activityAndBlob.Item1.ReplyToId,
                            };

                            await LogActivityToBlobClientAsync(tombstonedActivity, activityAndBlob.Item2, true).ConfigureAwait(false);
                        }

                        return;
                    }

                default:
                    await InnerLogActivityAsync(activity, false).ConfigureAwait(false);
                    return;
            }
        }

        /// <summary>
        /// Get activities for a conversation (Aka the transcript).
        /// </summary>
        /// <param name="channelId">Channel Id.</param>
        /// <param name="conversationId">Conversation Id.</param>
        /// <param name="continuationToken">Continuation token to page through results.</param>
        /// <param name="startDate">Earliest time to include.</param>
        /// <returns>PagedResult of activities.</returns>
        public async Task<PagedResult<IActivity>> GetTranscriptActivitiesAsync(string channelId, string conversationId, string continuationToken = null, DateTimeOffset startDate = default)
        {
            const int PageSize = 20;

            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            if (string.IsNullOrEmpty(conversationId))
            {
                throw new ArgumentNullException(nameof(conversationId));
            }

            var pagedResult = new PagedResult<IActivity>();

            string token = null;
            List<BlobItem> blobs = [];
            do
            {
                var resultSegment = _containerClient.Value
                    .GetBlobsAsync(BlobTraits.Metadata, prefix: $"{SanitizeKey(channelId)}/{SanitizeKey(conversationId)}/")
                    .AsPages(token).ConfigureAwait(false);
                
                token = null;
                await foreach (var blobPage in resultSegment)
                {
                    foreach (BlobItem blobItem in blobPage.Values)
                    {
                        if (DateTime.Parse(blobItem.Metadata["Timestamp"], CultureInfo.InvariantCulture).ToUniversalTime() >= startDate)
                        {
                            if (continuationToken != null)
                            {
                                if (blobItem.Name == continuationToken)
                                {
                                    // we found continuation token
                                    continuationToken = null;
                                }
                            }
                            else
                            {
                                blobs.Add(blobItem);
                                if (blobs.Count == PageSize)
                                {
                                    break;
                                }
                            }
                        }
                    }

                    // Get the continuation token and loop until it is empty.
                    token = blobPage.ContinuationToken;
                }
            }
            while (!string.IsNullOrEmpty(token) && blobs.Count < PageSize);

            pagedResult.Items = blobs
                .Select(async bl =>
                {
                    var blobClient = _containerClient.Value.GetBlobClient(bl.Name);
                    return await GetActivityFromBlobClientAsync(blobClient).ConfigureAwait(false);
                })
                .Select(t => t.Result)
                .ToArray();

            if (pagedResult.Items.Count == PageSize)
            {
                pagedResult.ContinuationToken = blobs.Last().Name;
            }

            return pagedResult;
        }

        /// <summary>
        /// List conversations in the channelId.
        /// </summary>
        /// <param name="channelId">Channel Id.</param>
        /// <param name="continuationToken">Continuation token to page through results.</param>
        /// <returns>A <see cref="Task"/> A task that represents the work queued to execute.</returns>
        public async Task<PagedResult<TranscriptInfo>> ListTranscriptsAsync(string channelId, string continuationToken = null)
        {
            const int PageSize = 20;

            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            string token = null;
            List<TranscriptInfo> conversations = [];
            do
            {
                var resultSegment = _containerClient.Value.GetBlobsAsync(BlobTraits.Metadata, prefix: $"{SanitizeKey(channelId)}/")
                                    .AsPages(token).ConfigureAwait(false);
                token = null;

                await foreach (var blobPage in resultSegment)
                {
                    foreach (BlobItem blobItem in blobPage.Values)
                    {
                        // Unescape the Id we escaped when we saved it
                        var conversation = new TranscriptInfo() { Id = Uri.UnescapeDataString(blobItem.Name.Split('/').Last(s => s.Length > 0)), ChannelId = channelId };
                        if (continuationToken != null)
                        {
                            if (conversation.Id == continuationToken)
                            {
                                // we found continuation token
                                continuationToken = null;
                            }

                            // skip record
                        }
                        else
                        {
                            conversations.Add(conversation);
                            if (conversations.Count == PageSize)
                            {
                                break;
                            }
                        }
                    }
                }
            }
            while (!string.IsNullOrEmpty(token) && conversations.Count < PageSize);

            var pagedResult = new PagedResult<TranscriptInfo>() { Items = conversations.ToArray() };

            if (pagedResult.Items.Count == PageSize)
            {
                pagedResult.ContinuationToken = pagedResult.Items.Last().Id;
            }

            return pagedResult;
        }

        /// <summary>
        /// Delete a specific conversation and all of it's activities.
        /// </summary>
        /// <param name="channelId">Channel Id where conversation took place.</param>
        /// <param name="conversationId">Id of the conversation to delete.</param>
        /// <returns>A <see cref="Task"/>A task that represents the work queued to execute.</returns>
        public async Task DeleteTranscriptAsync(string channelId, string conversationId)
        {
            if (string.IsNullOrEmpty(channelId))
            {
                throw new ArgumentNullException(nameof(channelId));
            }

            if (string.IsNullOrEmpty(conversationId))
            {
                throw new ArgumentNullException(nameof(conversationId));
            }

            string token = null;
            do
            {
                var resultSegment = _containerClient.Value
                    .GetBlobsAsync(BlobTraits.Metadata, prefix: $"{SanitizeKey(channelId)}/{SanitizeKey(conversationId)}/")
                    .AsPages(token).ConfigureAwait(false);

                token = null;

                await foreach (var blobPage in resultSegment)
                {
                    foreach (BlobItem blobItem in blobPage.Values)
                    {
                        var blobClient = _containerClient.Value.GetBlobClient(blobItem.Name);
                        await blobClient.DeleteIfExistsAsync().ConfigureAwait(false);
                    }

                    // Get the continuation token and loop until it is empty.
                    token = blobPage.ContinuationToken;
                }
            }
            while (!string.IsNullOrEmpty(token));
        }

        private async Task<(Activity, BlobClient)> InnerReadBlobAsync(IActivity activity)
        {
            var i = 0;
            while (true)
            {
                try
                {
                    string token = null;
                    do
                    {
                        var resultSegment = _containerClient.Value
                            .GetBlobsAsync(BlobTraits.Metadata, prefix: $"{SanitizeKey(activity.ChannelId)}/{SanitizeKey(activity.Conversation.Id)}/")
                            .AsPages(token).ConfigureAwait(false);

                        token = null;

                        await foreach (var blobPage in resultSegment)
                        {
                            foreach (BlobItem blobItem in blobPage.Values)
                            {
                                if (blobItem.Metadata.TryGetValue("Id", out string id) && id == activity.Id)
                                {
                                    var blobClient = _containerClient.Value.GetBlobClient(blobItem.Name);
                                    var blobActivity = await GetActivityFromBlobClientAsync(blobClient).ConfigureAwait(false);
                                    return (blobActivity, blobClient);
                                }
                            }

                            // Get the continuation token and loop until it is empty.
                            token = blobPage.ContinuationToken;
                        }
                    } 
                    while (!string.IsNullOrEmpty(token));

                    return default;
                }
                catch (RequestFailedException ex)
                    when ((HttpStatusCode)ex.Status == HttpStatusCode.PreconditionFailed)
                {
                    // additional retry logic, even though this is a read operation blob storage can return 412 if there is contention
                    if (i++ < 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                        continue;
                    }

                    throw;
                }
            }
        }

        private async Task<Activity> GetActivityFromBlobClientAsync(BlobClient blobClient)
        {
            using BlobDownloadInfo download = await blobClient.DownloadAsync().ConfigureAwait(false);
            if (download?.Content.Length == 0)
            {
                return null;
            }

            return JsonSerializer.Deserialize<Activity>(download.Content, _serializerOptions);
        }

        private Task InnerLogActivityAsync(IActivity activity, bool overwrite = true)
        {
            var blobName = GetBlobName(activity);
            var blobClient = _containerClient.Value.GetBlobClient(blobName);
            return LogActivityToBlobClientAsync(activity, blobClient, overwrite);
        }

        private async Task LogActivityToBlobClientAsync(IActivity activity, BlobClient blobClient, bool overwrite)
        {
            using var memoryStream = new MemoryStream();
            using var streamWriter = new StreamWriter(memoryStream);

            var metaData = new Dictionary<string, string>
            {
                ["Id"] = activity.Id,
                ["FromId"] = activity.From?.Id,
                ["RecipientId"] = activity.Recipient?.Id,
                ["Timestamp"] = activity.Timestamp.Value.ToString("O", CultureInfo.InvariantCulture)
            };

            var options = new BlobUploadOptions
            {
                Metadata = metaData,
                TransferOptions = _storageTransferOptions
            };

            if (!overwrite)
            {
                options.Conditions = new BlobRequestConditions { IfNoneMatch = new ETag("*") };
            }

            JsonSerializer.Serialize(memoryStream, activity, _serializerOptions);
            await streamWriter.FlushAsync().ConfigureAwait(false);
            memoryStream.Seek(0, SeekOrigin.Begin);

            try 
            {
                await blobClient.UploadAsync(memoryStream, options).ConfigureAwait(false);
            }
            catch (RequestFailedException ex)
                    when ((HttpStatusCode)ex.Status == HttpStatusCode.Conflict)
            {
                // ignore the conflict led by transient error when uploading
                if (overwrite || await blobClient.ExistsAsync().ConfigureAwait(false) == false)
                {
                    throw;
                }
            }
        }

        private static string GetBlobName(IActivity activity)
        {
            var blobName = $"{SanitizeKey(activity.ChannelId)}/{SanitizeKey(activity.Conversation.Id)}/{activity.Timestamp.Value.Ticks.ToString("x", CultureInfo.InvariantCulture)}-{SanitizeKey(activity.Id)}.json";
            return blobName;
        }

        private static string SanitizeKey(string key)
        {
            // Blob Name rules: case-sensitive any url char
            return Uri.EscapeDataString(key);
        }
    }
}
