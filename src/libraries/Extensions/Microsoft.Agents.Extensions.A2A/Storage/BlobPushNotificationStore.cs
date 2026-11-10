// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Extensions.A2A.Errors;
using Microsoft.AspNetCore.DataProtection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Storage;

/// <summary>
/// Persists encrypted <see cref="global::A2A.IPushNotificationStore"/> registrations in Azure Blob Storage.
/// </summary>
/// <remarks>
/// Use one live singleton per trusted authorization scope. Generation cancellation is process-local;
/// sharing the container does not provide multi-host coordination or durable notification delivery.
/// The host must authorize access, manage quotas and retention, and persist encryption keys separately
/// from registration records. Incoming tenant fields do not select the storage scope.
/// </remarks>
public sealed class BlobPushNotificationStore : IPushNotificationStore, IAsyncDisposable
{
    private const string ProtectionPurpose = "Microsoft.Agents.Extensions.A2A.Storage.BlobPushNotificationStore.v1";
    private const int MaxIdentifierBytes = 1024;
    private const int MaxCursorLength = 4096;
    private static readonly JsonSerializerOptions DocumentSerializerOptions = new(ProtocolJsonSerializer.SerializationOptions) { MaxDepth = 16 };
    private readonly BlobContainerClient _containerClient;
    private readonly IDataProtector _protector;
    private readonly string _blobPrefix;
    private readonly int _maxRegistrationsPerTask;
    private readonly int _maxConfigurationBytes;
    private readonly int _maxDocumentBytes;
    private readonly int _maxWriteAttempts;
    private readonly SemaphoreSlim[] _taskGates = Enumerable.Range(0, 32).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly ConcurrentDictionary<(string TaskId, string ConfigId), GenerationSignal> _signals = new();
    private readonly object _disposeLock = new();
    private Task _disposeTask;
    private int _disposed;

    /// <summary>
    /// Initializes a store for one trusted, single-active-host authorization scope.
    /// </summary>
    /// <param name="containerClient">The provisioned private container.</param>
    /// <param name="protectionProvider">The provider with externally managed, recoverable keys.</param>
    /// <param name="scope">The immutable authorization scope from trusted host configuration.</param>
    /// <param name="options">Optional resource limits and local-development transport settings.</param>
    public BlobPushNotificationStore(
        BlobContainerClient containerClient,
        IDataProtectionProvider protectionProvider,
        string scope,
        BlobPushNotificationStoreOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(containerClient);
        ArgumentNullException.ThrowIfNull(protectionProvider);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        options ??= new BlobPushNotificationStoreOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxRegistrationsPerTask);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxConfigurationBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxDocumentBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxWriteAttempts);

        var uri = containerClient.Uri;
        if (uri.Scheme != Uri.UriSchemeHttps &&
            !(options.AllowInsecureLoopback && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        {
            throw ExceptionHelper.GenerateException<ArgumentException>(ErrorHelper.PushNotificationTransportInsecure, null);
        }

        _containerClient = containerClient;
        _protector = protectionProvider.CreateProtector(ProtectionPurpose, scope);
        _blobPrefix = $"a2apush/{Hash(scope)}/";
        _maxRegistrationsPerTask = options.MaxRegistrationsPerTask;
        _maxConfigurationBytes = options.MaxConfigurationBytes;
        _maxDocumentBytes = options.MaxDocumentBytes;
        _maxWriteAttempts = options.MaxWriteAttempts;
    }

    /// <inheritdoc />
    public Task<PushNotificationConfigSnapshot> SaveAsync(TaskPushNotificationConfig config, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateIdentifier(config.TaskId);
        ValidateIdentifier(config.Id);
        var copy = CopyConfiguration(config);
        var generation = Guid.NewGuid().ToString("N");

        return WithTaskLockAsync(copy.TaskId, async retired =>
        {
            for (int attempt = 0; attempt < _maxWriteAttempts; attempt++)
            {
                var (document, etag) = await ReadAsync(copy.TaskId, cancellationToken).ConfigureAwait(false);
                if (document.Registrations.Any(entry => entry.Configuration.Id == copy.Id))
                {
                    throw new A2AException(ErrorHelper.PushNotificationAlreadyExists.description, A2AErrorCode.InvalidParams);
                }
                if (document.Registrations.Count >= _maxRegistrationsPerTask)
                {
                    throw LimitExceeded();
                }

                document.Registrations.Add(new StoredRegistration { Version = generation, Configuration = copy });
                if (await TryWriteAsync(copy.TaskId, document, etag, cancellationToken).ConfigureAwait(false))
                {
                    return CreateSnapshot(copy, generation, retired);
                }
            }
            throw WriteConflict();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<PushNotificationConfigSnapshot?> GetAsync(string taskId, string configId, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(taskId);
        ValidateIdentifier(configId);
        return WithTaskLockAsync<PushNotificationConfigSnapshot>(taskId, async retired =>
        {
            var (document, _) = await ReadAsync(taskId, cancellationToken).ConfigureAwait(false);
            var entry = document.Registrations.Find(value => value.Configuration.Id == configId);
            if (entry == null)
            {
                RetireSignal((taskId, configId), retired);
                return null;
            }
            return CreateSnapshot(entry.Configuration, entry.Version, retired);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PushNotificationConfigSnapshot>> GetAllAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(taskId);
        return WithTaskLockAsync<IReadOnlyList<PushNotificationConfigSnapshot>>(taskId, async retired =>
        {
            var (document, _) = await ReadAsync(taskId, cancellationToken).ConfigureAwait(false);
            return document.Registrations.OrderBy(entry => entry.Configuration.Id, StringComparer.Ordinal)
                .Select(entry => CreateSnapshot(entry.Configuration, entry.Version, retired)).ToArray();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<ListTaskPushNotificationConfigsResponse> ListAsync(ListTaskPushNotificationConfigsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var taskId = request.TaskId;
        ValidateIdentifier(taskId);
        int pageSize = request.PageSize.GetValueOrDefault(50);
        var pageToken = request.PageToken;
        if (pageSize < 0 || pageToken?.Length > MaxCursorLength)
        {
            throw InvalidParameters();
        }
        pageSize = Math.Min(pageSize == 0 ? 50 : pageSize, 100);

        return WithTaskLockAsync(taskId, async _ =>
        {
            var (document, _) = await ReadAsync(taskId, cancellationToken).ConfigureAwait(false);
            int offset = string.IsNullOrEmpty(pageToken) ? 0 : ReadCursor(pageToken, taskId, document);
            var configs = document.Registrations.OrderBy(entry => entry.Configuration.Id, StringComparer.Ordinal)
                .Skip(offset).Take(pageSize).Select(entry => entry.Configuration).ToList();
            int next = offset + configs.Count;
            return new ListTaskPushNotificationConfigsResponse
            {
                Configs = configs,
                NextPageToken = next < document.Registrations.Count
                    ? _protector.CreateProtector("cursor", taskId).Protect($"{document.Revision}\n{next.ToString(CultureInfo.InvariantCulture)}")
                    : string.Empty,
            };
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteAsync(string taskId, string configId, string version = null, CancellationToken cancellationToken = default)
    {
        ValidateIdentifier(taskId);
        ValidateIdentifier(configId);
        return WithTaskLockAsync(taskId, async retired =>
        {
            for (int attempt = 0; attempt < _maxWriteAttempts; attempt++)
            {
                var (document, etag) = await ReadAsync(taskId, cancellationToken).ConfigureAwait(false);
                var entry = document.Registrations.Find(value => value.Configuration.Id == configId);
                if (entry == null || (version != null && version != entry.Version))
                {
                    if (_signals.TryGetValue((taskId, configId), out var signal) && signal.Version != entry?.Version)
                    {
                        RetireSignal((taskId, configId), retired);
                    }
                    return false;
                }

                document.Registrations.Remove(entry);
                if (await TryWriteAsync(taskId, document, etag, cancellationToken).ConfigureAwait(false))
                {
                    RetireSignal((taskId, configId), retired);
                    return true;
                }
            }
            throw WriteConflict();
        }, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_disposeLock)
        {
            return new ValueTask(_disposeTask ??= DisposeCoreAsync());
        }
    }

    private async Task<T> WithTaskLockAsync<T>(
        string taskId, Func<List<(CancellationTokenSource Source, Task Completion)>, Task<T>> action, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var gate = _taskGates[(uint)StringComparer.Ordinal.GetHashCode(taskId) % (uint)_taskGates.Length];
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var retired = new List<(CancellationTokenSource Source, Task Completion)>();
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            return await action(retired).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
            await CompleteRetirementsAsync(retired).ConfigureAwait(false);
        }
    }

    private PushNotificationConfigSnapshot CreateSnapshot(
        TaskPushNotificationConfig configuration, string version, List<(CancellationTokenSource Source, Task Completion)> retired)
    {
        var key = (configuration.TaskId, configuration.Id);
        if (_signals.TryGetValue(key, out var signal) && signal.Version != version)
        {
            RetireSignal(key, retired);
            signal = null;
        }
        if (signal == null)
        {
            signal = new GenerationSignal(version, new CancellationTokenSource());
            _signals[key] = signal;
        }
        return new PushNotificationConfigSnapshot(configuration, version, signal.Source.Token);
    }

    private void RetireSignal((string TaskId, string ConfigId) key, List<(CancellationTokenSource Source, Task Completion)> retired)
    {
        if (_signals.TryRemove(key, out var signal))
        {
            // CancelAsync changes token state immediately; callbacks are awaited only after releasing the task gate.
            retired.Add((signal.Source, signal.Source.CancelAsync()));
        }
    }

    private static async Task CompleteRetirementsAsync(List<(CancellationTokenSource Source, Task Completion)> retired)
    {
        try
        {
            await Task.WhenAll(retired.Select(value => value.Completion)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.PushNotificationInvalidationFailed, null);
        }
        finally
        {
            foreach (var value in retired)
            {
                value.Source.Dispose();
            }
        }
    }

    private async Task DisposeCoreAsync()
    {
        Volatile.Write(ref _disposed, 1);
        foreach (var gate in _taskGates)
        {
            await gate.WaitAsync().ConfigureAwait(false);
        }
        var retired = new List<(CancellationTokenSource Source, Task Completion)>();
        try
        {
            foreach (var key in _signals.Keys)
            {
                RetireSignal(key, retired);
            }
        }
        finally
        {
            foreach (var gate in _taskGates)
            {
                gate.Release();
            }
        }
        await CompleteRetirementsAsync(retired).ConfigureAwait(false);
    }

    private async Task<(StoredTask Document, ETag? ETag)> ReadAsync(string taskId, CancellationToken cancellationToken)
    {
        BlobDownloadStreamingResult download;
        try
        {
            download = (await GetBlob(taskId).DownloadStreamingAsync(new BlobDownloadOptions(), cancellationToken).ConfigureAwait(false)).Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404 && exception.ErrorCode == BlobErrorCode.BlobNotFound.ToString())
        {
            return (new StoredTask { Format = 1, Revision = Guid.NewGuid().ToString("N"), Registrations = [] }, null);
        }

        await using var content = download.Content;
        if (download.Details.ContentLength < 0 || download.Details.ContentLength > _maxDocumentBytes ||
            string.IsNullOrEmpty(download.Details.ETag.ToString()))
        {
            throw InvalidData();
        }
        using var buffer = new MemoryStream();
        var bytes = new byte[Math.Min(81920, _maxDocumentBytes)];
        while (true)
        {
            int count = (int)Math.Min(bytes.Length, (long)_maxDocumentBytes - buffer.Length + 1);
            int read = await content.ReadAsync(bytes.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
            if (buffer.Length + read > _maxDocumentBytes)
            {
                throw InvalidData();
            }
            buffer.Write(bytes, 0, read);
        }
        if (buffer.Length != download.Details.ContentLength)
        {
            throw InvalidData();
        }
        cancellationToken.ThrowIfCancellationRequested();

        byte[] plaintext = null;
        try
        {
            plaintext = _protector.CreateProtector("document", taskId).Unprotect(buffer.ToArray());
            var document = JsonSerializer.Deserialize<StoredTask>(plaintext, DocumentSerializerOptions);
            ValidateDocument(document, taskId);
            return (document, download.Details.ETag);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or A2AException ||
            (exception is InvalidOperationException && exception.HResult == ErrorHelper.PushNotificationLimitExceeded.code))
        {
            throw InvalidData();
        }
        finally
        {
            if (plaintext != null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    private async Task<bool> TryWriteAsync(string taskId, StoredTask document, ETag? etag, CancellationToken cancellationToken)
    {
        document.Revision = Guid.NewGuid().ToString("N");
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(document, DocumentSerializerOptions);
        byte[] ciphertext;
        try
        {
            if (plaintext.Length > _maxDocumentBytes)
            {
                throw LimitExceeded();
            }
            cancellationToken.ThrowIfCancellationRequested();
            ciphertext = _protector.CreateProtector("document", taskId).Protect(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
        if (ciphertext.Length > _maxDocumentBytes)
        {
            throw LimitExceeded();
        }

        using var content = new MemoryStream(ciphertext, writable: false);
        try
        {
            await GetBlob(taskId).UploadAsync(content, new BlobUploadOptions
            {
                Conditions = etag.HasValue
                    ? new BlobRequestConditions { IfMatch = etag.Value }
                    : new BlobRequestConditions { IfNoneMatch = ETag.All },
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
            }, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException exception) when (
            (exception.Status == 412 && exception.ErrorCode == BlobErrorCode.ConditionNotMet.ToString()) ||
            (exception.Status == 409 && exception.ErrorCode == BlobErrorCode.BlobAlreadyExists.ToString()))
        {
            return false;
        }
    }

    private void ValidateDocument(StoredTask document, string taskId)
    {
        if (document?.Format != 1 || !Guid.TryParseExact(document.Revision, "N", out _) ||
            document.Registrations == null || document.Registrations.Count > _maxRegistrationsPerTask)
        {
            throw InvalidData();
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in document.Registrations)
        {
            if (entry?.Configuration == null || !Guid.TryParseExact(entry.Version, "N", out _) ||
                entry.Configuration.TaskId != taskId || !ids.Add(entry.Configuration.Id))
            {
                throw InvalidData();
            }
            ValidateIdentifier(entry.Configuration.Id);
            CopyConfiguration(entry.Configuration);
        }
    }

    private TaskPushNotificationConfig CopyConfiguration(TaskPushNotificationConfig configuration)
    {
        long characters = (long)(configuration.Id?.Length ?? 0) + (configuration.TaskId?.Length ?? 0) +
            (configuration.Url?.Length ?? 0) + (configuration.Token?.Length ?? 0) +
            (configuration.Tenant?.Length ?? 0) + (configuration.Authentication?.Scheme?.Length ?? 0) +
            (configuration.Authentication?.Credentials?.Length ?? 0);
        if (characters > _maxConfigurationBytes)
        {
            throw LimitExceeded();
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(configuration, A2AJsonUtilities.DefaultOptions);
        try
        {
            if (bytes.Length > _maxConfigurationBytes)
            {
                throw LimitExceeded();
            }
            return JsonSerializer.Deserialize<TaskPushNotificationConfig>(bytes, A2AJsonUtilities.DefaultOptions);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private int ReadCursor(string token, string taskId, StoredTask document)
    {
        try
        {
            var cursor = _protector.CreateProtector("cursor", taskId).Unprotect(token);
            var parts = cursor.Split('\n');
            if (parts.Length == 2 && parts[0] == document.Revision &&
                int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int offset) &&
                offset > 0 && offset < document.Registrations.Count)
            {
                return offset;
            }
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            throw InvalidParameters();
        }
        throw InvalidParameters();
    }

    private BlobClient GetBlob(string taskId) => _containerClient.GetBlobClient(_blobPrefix + Hash(taskId));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static void ValidateIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdentifierBytes ||
            value.Any(char.IsControl) || Encoding.UTF8.GetByteCount(value) > MaxIdentifierBytes)
        {
            throw InvalidParameters();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

    private static A2AException InvalidParameters() =>
        new(ErrorHelper.PushNotificationInvalidParameters.description, A2AErrorCode.InvalidParams);

    private static InvalidDataException InvalidData() =>
        ExceptionHelper.GenerateException<InvalidDataException>(ErrorHelper.PushNotificationDataInvalid, null);

    private static InvalidOperationException LimitExceeded() =>
        ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.PushNotificationLimitExceeded, null);

    private static InvalidOperationException WriteConflict() =>
        ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.PushNotificationWriteConflict, null);

    private sealed record GenerationSignal(string Version, CancellationTokenSource Source);

    private sealed class StoredTask
    {
        public int Format { get; set; }
        public string Revision { get; set; }
        public List<StoredRegistration> Registrations { get; set; }
    }

    private sealed class StoredRegistration
    {
        public string Version { get; set; }
        public TaskPushNotificationConfig Configuration { get; set; }
    }
}
