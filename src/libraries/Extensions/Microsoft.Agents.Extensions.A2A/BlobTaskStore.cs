// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Agents.Extensions.A2A;

/// <summary>
/// Implements <see cref="ITaskStore"/> using Azure Blob Storage.
/// </summary>
/// <remarks>
/// This implementation uses Azure Blob Storage to persist A2A tasks efficiently.
/// Each task is stored as a JSON blob with the key pattern "a2atask/{taskId}".
/// Supports efficient pagination via Azure Blob's native continuation tokens.
/// </remarks>
public class BlobTaskStore : ITaskStore
{
    private static readonly JsonSerializerOptions DefaultJsonSerializerOptions = ProtocolJsonSerializer.SerializationOptions;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly BlobContainerClient _containerClient;
    private int _checkForContainerExistence;
    private const string TaskPrefix = "a2atask/";

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobTaskStore"/> class.
    /// </summary>
    /// <param name="dataConnectionString">Azure Storage connection string.</param>
    /// <param name="containerName">Name of the Blob container where tasks will be stored.</param>
    /// <param name="jsonSerializerOptions">Custom JsonSerializerOptions.</param>
    public BlobTaskStore(string dataConnectionString, string containerName, JsonSerializerOptions jsonSerializerOptions = null)
    {
        AssertionHelpers.ThrowIfNullOrWhiteSpace(dataConnectionString, nameof(dataConnectionString));
        AssertionHelpers.ThrowIfNullOrWhiteSpace(containerName, nameof(containerName));

        _serializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;
        _checkForContainerExistence = 1;
        _containerClient = new BlobContainerClient(dataConnectionString, containerName);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobTaskStore"/> class.
    /// </summary>
    /// <param name="containerClient">The custom implementation of BlobContainerClient.</param>
    /// <param name="jsonSerializerOptions">Custom JsonSerializerOptions.</param>
    public BlobTaskStore(BlobContainerClient containerClient, JsonSerializerOptions jsonSerializerOptions = null)
    {
        AssertionHelpers.ThrowIfNull(containerClient, nameof(containerClient));

        _containerClient = containerClient;
        _serializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;
        _checkForContainerExistence = 1;
    }

    /// <inheritdoc />
    public async Task<AgentTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrEmpty(taskId, nameof(taskId));
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

        var blobName = GetBlobName(taskId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            return await DownloadTaskAsync(blobClient, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when ((HttpStatusCode)ex.Status == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task SaveTaskAsync(string taskId, AgentTask task, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(task, nameof(task));
        AssertionHelpers.ThrowIfNullOrEmpty(taskId, nameof(taskId));
        cancellationToken.ThrowIfCancellationRequested();

        task.Id ??= taskId;

        await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

        var blobName = GetBlobName(taskId);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            var jsonObject = JsonObject.Create(JsonSerializer.SerializeToElement(task, _serializerOptions));
            if (jsonObject != null)
            {
                using var memoryStream = new MemoryStream();

                // Retain type info
                jsonObject.AddTypeInfo(task);

                JsonSerializer.Serialize(memoryStream, jsonObject, _serializerOptions);
                memoryStream.Seek(0, SeekOrigin.Begin);

                var blobHttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json"
                };

                await blobClient.UploadAsync(
                    memoryStream,
                    httpHeaders: blobHttpHeaders,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.BadRequest && ex.ErrorCode == BlobErrorCode.InvalidBlockList)
        {
            throw new InvalidOperationException(
                $"An error occurred while trying to write a task. The underlying '{BlobErrorCode.InvalidBlockList}' error is commonly caused due to concurrently uploading an object larger than 128MB in size.",
                ex);
        }
    }

    /// <inheritdoc />
    public async Task DeleteTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNullOrEmpty(taskId, nameof(taskId));
        cancellationToken.ThrowIfCancellationRequested();

        var blobName = GetBlobName(taskId);
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ListTasksResponse> ListTasksAsync(ListTasksRequest request, CancellationToken cancellationToken = default)
    {
        AssertionHelpers.ThrowIfNull(request, nameof(request));
        cancellationToken.ThrowIfCancellationRequested();

        await EnsureContainerExistsAsync(cancellationToken).ConfigureAwait(false);

        var tasks = new List<AgentTask>();

        // Use Azure Blob's native pagination with continuation tokens
        // AsPages() fetches only one page at a time
        var pages = _containerClient
            .GetBlobsAsync(prefix: TaskPrefix, cancellationToken: cancellationToken)
            .AsPages(continuationToken: request.PageToken, pageSizeHint: request.PageSize);

        // Get first page only (efficient!)
        await using var enumerator = pages.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            // No blobs found
            return new ListTasksResponse
            {
                Tasks = tasks
            };
        }

        var page = enumerator.Current;

        // Download only blobs in this page
        foreach (var blobItem in page.Values)
        {
            var blobClient = _containerClient.GetBlobClient(blobItem.Name);
            var task = await DownloadTaskAsync(blobClient, cancellationToken).ConfigureAwait(false);

            // Apply filters (Status, ContextId, etc.)
            if (ShouldIncludeTask(task, request))
            {
                tasks.Add(task);
            }
        }

        return new ListTasksResponse
        {
            Tasks = tasks,
            NextPageToken = page.ContinuationToken
        };
    }

    private static bool ShouldIncludeTask(AgentTask task, ListTasksRequest request)
    {
        if (request.Status.HasValue && task.Status?.State != request.Status.Value)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(request.ContextId) && task.ContextId != request.ContextId)
        {
            return false;
        }

        return true;
    }

    private static string GetBlobName(string taskId)
    {
        if (string.IsNullOrEmpty(taskId))
        {
            throw new ArgumentNullException(nameof(taskId));
        }

        return HttpUtility.UrlEncode($"{TaskPrefix}{taskId}");
    }

    private async Task<AgentTask> DownloadTaskAsync(BlobClient blobClient, CancellationToken cancellationToken)
    {
        using BlobDownloadInfo download = await blobClient.DownloadAsync(cancellationToken).ConfigureAwait(false);

        using var sr = new StreamReader(download.Content);
        var json = await sr.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var jsonObject = (JsonObject)JsonObject.Parse(json);

        if (jsonObject.GetTypeInfo(out var type))
        {
            var typeProps = jsonObject.RemoveTypeInfoProperties();
            var task = jsonObject.Deserialize(type, _serializerOptions) as AgentTask;
            jsonObject.SetTypeInfoProperties(typeProps);
            return task ?? throw new InvalidDataException("Unexpected response content. Unable to deserialize as AgentTask.");
        }
        else
        {
            return jsonObject.Deserialize<AgentTask>(_serializerOptions)
                ?? throw new InvalidDataException("Unexpected response content. Unable to deserialize as AgentTask.");
        }
    }

    private async Task EnsureContainerExistsAsync(CancellationToken cancellationToken)
    {
        // This should only happen once - assuming this is a singleton
        if (Interlocked.CompareExchange(ref _checkForContainerExistence, 0, 1) == 1)
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
