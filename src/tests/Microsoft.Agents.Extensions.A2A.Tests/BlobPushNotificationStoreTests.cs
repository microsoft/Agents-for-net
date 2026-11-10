// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Agents.Extensions.A2A.Storage;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class BlobPushNotificationStoreTests
{
    [Fact]
    public async Task SaveAsync_EncryptsSecretsAndRecoversAfterStoreRestart()
    {
        var storage = new TestBlobStorage();
        var directory = Directory.CreateTempSubdirectory("a2a-push-store-");
        try
        {
            var input = CreateConfig("config-a");
            string version;
            await using (var store = new BlobPushNotificationStore(
                storage.Client, DataProtectionProvider.Create(directory), "trusted-scope"))
            {
                var saved = await store.SaveAsync(input);
                version = saved.Version;
                input.Token = "changed";
                saved.Configuration.Authentication.Credentials = "changed";
            }

            var persisted = Encoding.UTF8.GetString(storage.GetContent(storage.LastWrittenName));
            foreach (var secret in new[] { "callback-token", "callback-credential", "callbacks.example", "task-a", "config-a", "trusted-scope", "routing-tenant" })
            {
                Assert.DoesNotContain(secret, persisted);
                Assert.DoesNotContain(secret, storage.LastWrittenName);
            }

            await using var reopened = new BlobPushNotificationStore(
                storage.Client, DataProtectionProvider.Create(directory), "trusted-scope");
            var recovered = await reopened.GetAsync("task-a", "config-a");

            Assert.NotEmpty(version);
            Assert.Equal(version, recovered.Version);
            Assert.Equal("callback-token", recovered.Configuration.Token);
            Assert.Equal("callback-credential", recovered.Configuration.Authentication.Credentials);
            Assert.Equal("https://callbacks.example/notify", recovered.Configuration.Url);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveAsync_ConditionalWritesPreventDuplicateOrLostRegistrations(bool duplicateId)
    {
        var storage = new TestBlobStorage();
        var keys = new EphemeralDataProtectionProvider();
        await using var first = CreateStore(storage, keys);
        await using var second = CreateStore(storage, keys);
        var uploadsReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int uploads = 0;
        storage.BeforeUploadAsync = () =>
        {
            if (Interlocked.Increment(ref uploads) == 2)
            {
                uploadsReady.SetResult();
            }
            return uploadsReady.Task;
        };

        async Task<bool> SaveAsync(BlobPushNotificationStore store, string id)
        {
            try
            {
                await store.SaveAsync(CreateConfig(id));
                return true;
            }
            catch (A2AException exception) when (exception.ErrorCode == A2AErrorCode.InvalidParams)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(
            SaveAsync(first, "config-a"),
            SaveAsync(second, duplicateId ? "config-a" : "config-b"))
            .WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(duplicateId ? 1 : 2, results.Count(result => result));
        var persisted = await first.GetAllAsync("task-a");
        Assert.Equal(duplicateId ? new[] { "config-a" } : new[] { "config-a", "config-b" },
            persisted.Select(snapshot => snapshot.Configuration.Id));
    }

    [Fact]
    public async Task DeleteAsync_CancelsLocalGenerationWithoutDeletingItsReplacement()
    {
        var storage = new TestBlobStorage();
        await using var store = CreateStore(storage);
        var original = await store.SaveAsync(CreateConfig("config-a"));
        var other = await store.SaveAsync(CreateConfig("config-b"));
        var fetched = await store.GetAsync("task-a", "config-a");
        var all = await store.GetAllAsync("task-a");

        await store.DeleteAsync("task-a", "config-a", original.Version);

        Assert.True(original.RemovalToken.IsCancellationRequested);
        Assert.True(fetched.RemovalToken.IsCancellationRequested);
        Assert.True(all[0].RemovalToken.IsCancellationRequested);
        Assert.False(other.RemovalToken.IsCancellationRequested);
        Assert.Null(await store.GetAsync("task-a", "config-a"));

        var replacement = await store.SaveAsync(CreateConfig("config-a"));
        await store.DeleteAsync("task-a", "config-a", original.Version);

        Assert.NotEqual(original.Version, replacement.Version);
        Assert.False(replacement.RemovalToken.IsCancellationRequested);
        Assert.Equal(replacement.Version, (await store.GetAsync("task-a", "config-a")).Version);

        await store.DeleteAsync("task-a", "config-a");
        await store.DeleteAsync("task-a", "config-a");
        Assert.True(replacement.RemovalToken.IsCancellationRequested);
    }

    [Fact]
    public async Task DeleteAsync_CancellationCallbacksCanReenterTheStore()
    {
        await using var store = CreateStore(new TestBlobStorage());
        var saved = await store.SaveAsync(CreateConfig("config-a"));
        using var callback = saved.RemovalToken.Register(() =>
        {
            Assert.Null(store.GetAsync("task-a", "config-a").GetAwaiter().GetResult());
        });

        await store.DeleteAsync("task-a", "config-a").WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(saved.RemovalToken.IsCancellationRequested);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsIndependentTaskScopedSnapshots()
    {
        await using var store = CreateStore(new TestBlobStorage());
        await store.SaveAsync(CreateConfig("config-b"));
        await store.SaveAsync(CreateConfig("config-a"));
        await store.SaveAsync(CreateConfig("config-other", "another-task"));

        var all = await store.GetAllAsync("task-a");
        all[0].Configuration.Token = "changed";

        Assert.Equal(new[] { "config-a", "config-b" }, all.Select(snapshot => snapshot.Configuration.Id));
        Assert.Equal("callback-token", (await store.GetAsync("task-a", "config-a")).Configuration.Token);
        Assert.Empty(await store.GetAllAsync("absent-task"));
    }

    [Fact]
    public async Task ListAsync_ProtectsTaskScopedRevisionBoundPagination()
    {
        var storage = new TestBlobStorage();
        var keys = new EphemeralDataProtectionProvider();
        await using var store = CreateStore(storage, keys);
        foreach (var id in new[] { "config-c", "config-a", "config-b" })
        {
            await store.SaveAsync(CreateConfig(id));
        }

        var first = await store.ListAsync(new() { TaskId = "task-a", PageSize = 1 });
        var second = await store.ListAsync(new() { TaskId = "task-a", PageSize = 1, PageToken = first.NextPageToken });
        var last = await store.ListAsync(new() { TaskId = "task-a", PageSize = 1, PageToken = second.NextPageToken });

        Assert.Equal("config-a", Assert.Single(first.Configs).Id);
        Assert.Equal("config-b", Assert.Single(second.Configs).Id);
        Assert.Equal("config-c", Assert.Single(last.Configs).Id);
        Assert.True(string.IsNullOrEmpty(last.NextPageToken));
        Assert.DoesNotContain("task-a", first.NextPageToken);

        foreach (var request in new[]
        {
            new ListTaskPushNotificationConfigsRequest { TaskId = "task-a", PageToken = first.NextPageToken + "!" },
            new ListTaskPushNotificationConfigsRequest { TaskId = "another-task", PageToken = first.NextPageToken },
        })
        {
            var exception = await Assert.ThrowsAsync<A2AException>(() => store.ListAsync(request));
            Assert.Equal(A2AErrorCode.InvalidParams, exception.ErrorCode);
        }

        await using var otherScope = CreateStore(storage, keys, scope: "another-scope");
        await Assert.ThrowsAsync<A2AException>(() => otherScope.ListAsync(
            new() { TaskId = "task-a", PageToken = first.NextPageToken }));

        await store.DeleteAsync("task-a", "config-b");
        await Assert.ThrowsAsync<A2AException>(() => store.ListAsync(
            new() { TaskId = "task-a", PageToken = first.NextPageToken }));
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("other-task")]
    [InlineData("other-scope")]
    [InlineData("other-keys")]
    public async Task GetAsync_RejectsCorruptOrTransplantedCiphertextWithoutExposingSecrets(string attack)
    {
        var storage = new TestBlobStorage();
        var keys = new EphemeralDataProtectionProvider();
        await using var original = CreateStore(storage, keys);
        await original.SaveAsync(CreateConfig("config-a"));
        var content = storage.GetContent(storage.LastWrittenName);
        var taskId = attack == "other-task" ? "another-task" : "task-a";
        await using var target = CreateStore(storage,
            attack == "other-keys" ? new EphemeralDataProtectionProvider() : keys,
            scope: attack == "other-scope" ? "another-scope" : "trusted-scope");

        if (attack == "other-task" || attack == "other-scope")
        {
            await target.SaveAsync(CreateConfig("config-a", taskId));
        }
        if (attack == "tampered")
        {
            content[content.Length / 2] ^= 1;
        }
        storage.SetContent(storage.LastWrittenName, content);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => target.GetAsync(taskId, "config-a"));

        Assert.DoesNotContain("callback-token", exception.ToString());
        Assert.DoesNotContain("callback-credential", exception.ToString());
        Assert.DoesNotContain("callbacks.example", exception.ToString());
    }

    [Theory]
    [InlineData("get", "ContainerNotFound", 404)]
    [InlineData("get-all", "ContainerNotFound", 404)]
    [InlineData("list", "ContainerNotFound", 404)]
    [InlineData("save", "ContainerNotFound", 404)]
    [InlineData("delete", "ContainerNotFound", 404)]
    [InlineData("get", "ServerBusy", 503)]
    [InlineData("get-all", "ServerBusy", 503)]
    [InlineData("list", "ServerBusy", 503)]
    [InlineData("save", "ServerBusy", 503)]
    [InlineData("delete", "ServerBusy", 503)]
    public async Task StorageFailures_AreNotReportedAsAbsentOrSuccessful(string operation, string errorCode, int status)
    {
        var storage = new TestBlobStorage { ReadFailure = new RequestFailedException(status, "Storage unavailable.", errorCode, null) };
        await using var store = CreateStore(storage);

        var exception = await Assert.ThrowsAsync<RequestFailedException>(() => ExecuteAsync(store, operation));

        Assert.Equal(status, exception.Status);
    }

    [Fact]
    public async Task DeleteAsync_FailedWriteDoesNotCancelOrRemoveRegistration()
    {
        var storage = new TestBlobStorage();
        await using var store = CreateStore(storage);
        var saved = await store.SaveAsync(CreateConfig("config-a"));
        storage.WriteFailure = new RequestFailedException(503, "Storage unavailable.");

        await Assert.ThrowsAsync<RequestFailedException>(() => store.DeleteAsync("task-a", "config-a"));

        Assert.False(saved.RemovalToken.IsCancellationRequested);
        Assert.Equal(saved.Version, (await store.GetAsync("task-a", "config-a")).Version);
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("registrations")]
    [InlineData("document")]
    public async Task SaveAsync_EnforcesLimitsWithoutPersistingRejectedSecrets(string limit)
    {
        var storage = new TestBlobStorage();
        var options = new BlobPushNotificationStoreOptions
        {
            MaxConfigurationBytes = 1024,
            MaxRegistrationsPerTask = 1,
            MaxDocumentBytes = limit == "document" ? 128 : 8192,
        };
        await using var store = CreateStore(storage, options: options);
        if (limit == "registrations")
        {
            await store.SaveAsync(CreateConfig("config-a"));
        }
        var input = CreateConfig("config-b");
        if (limit == "configuration")
        {
            input.Token = new string('s', 2048);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(input));

        Assert.Null(await store.GetAsync("task-a", "config-b"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetAsync_BoundsUntrustedDownloadsAndDisposesTheStream(bool incorrectContentLength)
    {
        var storage = new TestBlobStorage();
        await using var store = CreateStore(storage, options: new() { MaxDocumentBytes = 1024 });
        await store.SaveAsync(CreateConfig("config-a"));
        storage.SetContent(storage.LastWrittenName, new byte[16384]);
        storage.ReportedLength = incorrectContentLength ? 1 : null;

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync("task-a", "config-a"));

        Assert.True(storage.LastDownload.Disposed);
        Assert.InRange(storage.LastDownload.BytesRead, 0, incorrectContentLength ? 1025 : 0);
    }

    [Fact]
    public async Task SaveAsync_StopsAfterBoundedWriteContention()
    {
        var storage = new TestBlobStorage { WriteConflicts = 10 };
        await using var store = CreateStore(storage, options: new() { MaxWriteAttempts = 2 });

        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync(CreateConfig("config-a")));

        Assert.Equal(2, storage.UploadAttempts);
        Assert.Null(await store.GetAsync("task-a", "config-a"));
    }

    [Theory]
    [InlineData("get")]
    [InlineData("get-all")]
    [InlineData("list")]
    [InlineData("save")]
    [InlineData("delete")]
    public async Task Operations_HonorCallerCancellation(string operation)
    {
        await using var store = CreateStore(new TestBlobStorage());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ExecuteAsync(store, operation, cancellation.Token));
    }

    [Fact]
    public async Task DisposeAsync_CancelsHeldSnapshotsAndRejectsNewOperations()
    {
        var store = CreateStore(new TestBlobStorage());
        var saved = await store.SaveAsync(CreateConfig("config-a"));

        await store.DisposeAsync();
        await store.DisposeAsync();

        Assert.True(saved.RemovalToken.IsCancellationRequested);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => store.GetAsync("task-a", "config-a"));
    }

    [Theory]
    [InlineData("http://storage.example/push", false)]
    [InlineData("http://storage.example/push", true)]
    [InlineData("http://127.0.0.1:10000/devstoreaccount1/push", false)]
    public void Constructor_RejectsUnencryptedNonDevelopmentTransport(string uri, bool allowInsecureLoopback)
    {
        var storage = new TestBlobStorage(new Uri(uri));

        Assert.Throws<ArgumentException>(() => CreateStore(storage,
            options: new() { AllowInsecureLoopback = allowInsecureLoopback }));
    }

    [Fact]
    public async Task SaveAsync_AllowsExplicitLoopbackDevelopmentTransport()
    {
        var storage = new TestBlobStorage(new Uri("http://127.0.0.1:10000/devstoreaccount1/push"));
        await using var store = CreateStore(storage, options: new() { AllowInsecureLoopback = true });

        await store.SaveAsync(CreateConfig("config-a"));

        Assert.NotNull(await store.GetAsync("task-a", "config-a"));
    }

    private static BlobPushNotificationStore CreateStore(
        TestBlobStorage storage,
        IDataProtectionProvider keys = null,
        string scope = "trusted-scope",
        BlobPushNotificationStoreOptions options = null) =>
        new(storage.Client, keys ?? new EphemeralDataProtectionProvider(), scope, options);

    private static TaskPushNotificationConfig CreateConfig(string id, string taskId = "task-a") => new()
    {
        Id = id,
        TaskId = taskId,
        Url = "https://callbacks.example/notify",
        Token = "callback-token",
        Authentication = new() { Scheme = "Bearer", Credentials = "callback-credential" },
        Tenant = "routing-tenant",
    };

    private static Task ExecuteAsync(BlobPushNotificationStore store, string operation, CancellationToken cancellationToken = default) =>
        operation switch
        {
            "get" => store.GetAsync("task-a", "config-a", cancellationToken),
            "get-all" => store.GetAllAsync("task-a", cancellationToken),
            "list" => store.ListAsync(new() { TaskId = "task-a" }, cancellationToken),
            "save" => store.SaveAsync(CreateConfig("config-a"), cancellationToken),
            "delete" => store.DeleteAsync("task-a", "config-a", cancellationToken: cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

    private sealed class TestBlobStorage
    {
        private readonly object _sync = new();
        private readonly Dictionary<string, (byte[] Content, ETag ETag)> _blobs = new(StringComparer.Ordinal);
        private int _revision;

        public TestBlobStorage(Uri uri = null)
        {
            var container = new Mock<BlobContainerClient>();
            container.SetupGet(client => client.Uri).Returns(uri ?? new Uri("https://storage.example/push"));
            container.Setup(client => client.GetBlobClient(It.IsAny<string>())).Returns((string name) => CreateBlob(name));
            Client = container.Object;
        }

        public BlobContainerClient Client { get; }
        public string LastWrittenName { get; private set; }
        public TrackingStream LastDownload { get; private set; }
        public RequestFailedException ReadFailure { get; set; }
        public RequestFailedException WriteFailure { get; set; }
        public Func<Task> BeforeUploadAsync { get; set; }
        public long? ReportedLength { get; set; }
        public int WriteConflicts { get; set; }
        public int UploadAttempts { get; private set; }

        public byte[] GetContent(string name)
        {
            lock (_sync)
            {
                return _blobs[name].Content.ToArray();
            }
        }

        public void SetContent(string name, byte[] content)
        {
            lock (_sync)
            {
                _blobs[name] = (content.ToArray(), new ETag($"\"{++_revision}\""));
                LastWrittenName = name;
            }
        }

        private BlobClient CreateBlob(string name)
        {
            var blob = new Mock<BlobClient>();
            blob.Setup(client => client.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
                .Returns((BlobDownloadOptions _, CancellationToken token) =>
                {
                    token.ThrowIfCancellationRequested();
                    if (ReadFailure != null)
                    {
                        throw ReadFailure;
                    }
                    lock (_sync)
                    {
                        if (!_blobs.TryGetValue(name, out var stored))
                        {
                            throw new RequestFailedException(404, "Blob absent.", "BlobNotFound", null);
                        }
                        LastDownload = new TrackingStream(stored.Content.ToArray());
                        return Task.FromResult(Response.FromValue(
                            BlobsModelFactory.BlobDownloadStreamingResult(LastDownload,
                                BlobsModelFactory.BlobDownloadDetails(
                                    contentLength: ReportedLength ?? stored.Content.Length, eTag: stored.ETag)),
                            Mock.Of<Response>()));
                    }
                });
            blob.Setup(client => client.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
                .Returns(async (Stream stream, BlobUploadOptions options, CancellationToken token) =>
                {
                    using var content = new MemoryStream();
                    await stream.CopyToAsync(content, token);
                    if (BeforeUploadAsync != null)
                    {
                        await BeforeUploadAsync();
                    }
                    token.ThrowIfCancellationRequested();
                    lock (_sync)
                    {
                        UploadAttempts++;
                        if (WriteFailure != null)
                        {
                            throw WriteFailure;
                        }
                        if (WriteConflicts-- > 0 ||
                            (_blobs.TryGetValue(name, out var existing)
                                ? options.Conditions?.IfNoneMatch == ETag.All || options.Conditions?.IfMatch != existing.ETag
                                : options.Conditions?.IfNoneMatch != ETag.All))
                        {
                            throw new RequestFailedException(412, "Write condition failed.", "ConditionNotMet", null);
                        }
                        SetContent(name, content.ToArray());
                        return Response.FromValue(
                            BlobsModelFactory.BlobContentInfo(
                                _blobs[name].ETag, DateTimeOffset.UtcNow, null, null, null, null, 0),
                            Mock.Of<Response>());
                    }
                });
            return blob.Object;
        }
    }

    private sealed class TrackingStream(byte[] content) : MemoryStream(content)
    {
        public int BytesRead { get; private set; }
        public bool Disposed { get; private set; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await base.ReadAsync(buffer, cancellationToken);
            BytesRead += read;
            return read;
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
