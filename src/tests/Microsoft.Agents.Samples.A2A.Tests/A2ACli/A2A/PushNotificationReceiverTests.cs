// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2ACli.A2A;
using Xunit;

namespace Microsoft.Agents.Samples.A2ACli.Tests.A2A;

public sealed class PushNotificationReceiverTests
{
    [Fact]
    public async Task StartAsync_ReceivesStreamResponse()
    {
        var received = new TaskCompletionSource<StreamResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var receiver = await PushNotificationReceiver.StartAsync(
            new Uri("http://127.0.0.1:0"),
            notification => received.TrySetResult(notification),
            CancellationToken.None);
        using var client = new HttpClient();
        var notification = new StreamResponse
        {
            StatusUpdate = new TaskStatusUpdateEvent
            {
                TaskId = "task-1",
                ContextId = "context-1",
                Status = new global::A2A.TaskStatus { State = TaskState.Completed },
            },
        };

        using HttpResponseMessage response = await client.PostAsync(
            receiver.NotificationUri,
            CreateJsonContent(notification),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        StreamResponse actual = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(StreamResponseCase.StatusUpdate, actual.PayloadCase);
        Assert.Equal("task-1", actual.StatusUpdate?.TaskId);
    }

    [Fact]
    public async Task StartAsync_MalformedJson_ReturnsBadRequest()
    {
        bool notificationReceived = false;
        await using var receiver = await PushNotificationReceiver.StartAsync(
            new Uri("http://127.0.0.1:0"),
            _ => notificationReceived = true,
            CancellationToken.None);
        using var client = new HttpClient();

        using HttpResponseMessage response = await client.PostAsync(
            receiver.NotificationUri,
            new StringContent("{", Encoding.UTF8, "application/json"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(notificationReceived);
    }

    [Fact]
    public async Task StartAsync_BasePath_AppendsNotifyPath()
    {
        await using var receiver = await PushNotificationReceiver.StartAsync(
            new Uri("http://127.0.0.1:0/callbacks/"),
            _ => { },
            CancellationToken.None);

        Assert.Equal("/callbacks/notify", receiver.NotificationUri.AbsolutePath);
        Assert.NotEqual(0, receiver.NotificationUri.Port);
    }

    [Fact]
    public async Task DisposeAsync_StopsReceiver()
    {
        var receiver = await PushNotificationReceiver.StartAsync(
            new Uri("http://127.0.0.1:0"),
            _ => { },
            CancellationToken.None);
        using var client = new HttpClient();
        Uri notificationUri = receiver.NotificationUri;

        await receiver.DisposeAsync();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.PostAsync(
                notificationUri,
                CreateJsonContent(new StreamResponse()),
                CancellationToken.None));
    }

    private static StringContent CreateJsonContent(StreamResponse notification) =>
        new(
            JsonSerializer.Serialize(notification, A2AJsonUtilities.DefaultOptions),
            Encoding.UTF8,
            "application/json");
}
