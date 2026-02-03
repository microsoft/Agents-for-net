// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Hosting.AspNetCore.A2A;

/// <summary>
/// An A2A Adapter using Http.
/// </summary>
public interface IA2AHttpAdapter : IAgentHttpAdapter
{
    Task<IResult> ProcessJsonRpcAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default);

    Task<IResult> GetTaskAsync(HttpRequest httpRequest, HttpResponse response, IAgent agent, string id, int? historyLength, string? metadata, CancellationToken cancellationToken = default);
    Task<IResult> CancelTaskAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default);
    Task<IResult> SendMessageAsync(HttpRequest httpRequest, HttpResponse response, IAgent agent, MessageSendParams sendParams, CancellationToken cancellationToken = default);
    IResult SendMessageStream(HttpRequest httpRequest, HttpResponse response, IAgent agent, MessageSendParams sendParams, CancellationToken cancellationToken = default);
    IResult SubscribeToTask(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default);
    Task<IResult> SetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, PushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default);
    Task<IResult> GetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, string? notificationConfigId, CancellationToken cancellationToken = default);

    Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string pathPrefix, CancellationToken cancellationToken = default);
}
