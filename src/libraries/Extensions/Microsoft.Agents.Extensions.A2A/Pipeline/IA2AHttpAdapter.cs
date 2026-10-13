// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Hosting.AspNetCore;
using Microsoft.AspNetCore.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;

namespace Microsoft.Agents.Extensions.A2A.Pipeline;

/// <summary>
/// Defines HTTP endpoint handlers for the A2A protocol.
/// </summary>
internal interface IA2AHttpAdapter : IAgentHttpAdapter
{
    /// <summary>
    /// Processes an A2A JSON-RPC request.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that handles the request.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The result to write to the HTTP response.</returns>
    Task<IResult> ProcessJsonRpcAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the Agent Card for an A2A agent.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response that receives the Agent Card.</param>
    /// <param name="agent">The agent whose card is requested.</param>
    /// <param name="pathPrefix">The URL path prefix for the A2A endpoint.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>A task that completes after the Agent Card is written.</returns>
    Task ProcessAgentCardAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string pathPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an A2A task.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that owns the task.</param>
    /// <param name="id">The task identifier.</param>
    /// <param name="historyLength">The optional number of historical messages to include.</param>
    /// <param name="metadata">The optional task metadata filter.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The result to write to the HTTP response.</returns>
    Task<IResult> GetTaskAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, int? historyLength, string? metadata, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an A2A task.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that owns the task.</param>
    /// <param name="id">The task identifier.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The result to write to the HTTP response.</returns>
    Task<IResult> CancelTaskAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a non-streaming A2A message.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that handles the message.</param>
    /// <param name="sendRequest">The A2A message request.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The result to write to the HTTP response.</returns>
    Task<IResult> SendMessageAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, SendMessageRequest sendRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes a task-scoped authorization request.
    /// </summary>
    Task<IResult> ResumeAuthAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string taskId, ResumeAuthRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an A2A message and returns a streaming result.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that handles the message.</param>
    /// <param name="sendRequest">The A2A message request.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The streaming result to write to the HTTP response.</returns>
    IResult SendMessageStream(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, SendMessageRequest sendRequest, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to streaming updates for an A2A task.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that owns the task.</param>
    /// <param name="id">The task identifier.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The streaming result to write to the HTTP response.</returns>
    IResult SubscribeToTask(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a push-notification configuration for an A2A task.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that owns the task.</param>
    /// <param name="id">The task identifier.</param>
    /// <param name="pushNotificationConfig">The configuration to create.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The result to write to the HTTP response.</returns>
    Task<IResult> SetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, PushNotificationConfig pushNotificationConfig, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a push-notification configuration for an A2A task.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that owns the task.</param>
    /// <param name="id">The task identifier.</param>
    /// <param name="notificationConfigId">The optional push-notification configuration identifier.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The result to write to the HTTP response.</returns>
    Task<IResult> GetPushNotificationAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, string? notificationConfigId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists push-notification configurations for an A2A task.
    /// </summary>
    /// <param name="httpRequest">The incoming HTTP request.</param>
    /// <param name="httpResponse">The HTTP response to populate when required.</param>
    /// <param name="agent">The agent that owns the task.</param>
    /// <param name="id">The task identifier.</param>
    /// <param name="pageSize">The optional maximum number of configurations to return.</param>
    /// <param name="pageToken">The optional continuation token.</param>
    /// <param name="cancellationToken">A token used to cancel processing.</param>
    /// <returns>The result to write to the HTTP response.</returns>
    Task<IResult> ListPushNotificationConfigsAsync(HttpRequest httpRequest, HttpResponse httpResponse, IAgent agent, string id, int? pageSize, string? pageToken, CancellationToken cancellationToken);
}
