// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using Microsoft.AspNetCore.Http;
using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;

internal static class InTaskAuthorizationOperation
{
    private static readonly A2AOperationId OperationId =
        new(InTaskAuthorizationExtension.ResumeAuthOperationId);

    internal static A2AOperation<ResumeAuthRequest, AgentTask> AddOperation(
        A2AOperationCatalogBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.DefineUnary<ResumeAuthRequest, AgentTask>(
            OperationId,
            ValidateRequest);
    }

    internal static void AddJsonRpcBinding(
        A2AJsonRpcOperationBindingBuilder builder,
        A2AOperation<ResumeAuthRequest, AgentTask> operation)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(operation);
        builder.Map(
            InTaskAuthorizationExtension.ResumeAuthOperation,
            operation,
            InTaskAuthorizationJsonContext.Default.ResumeAuthRequest,
            GetA2ATypeInfo<AgentTask>());
    }

    internal static void AddHttpBinding(
        A2AHttpOperationBindingBuilder builder,
        A2AOperation<ResumeAuthRequest, AgentTask> operation)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(operation);
        builder.Map(
            HttpMethods.Post,
            "/tasks/{taskId}:resumeAuth",
            operation,
            BindHttpRequestAsync,
            GetA2ATypeInfo<AgentTask>());
    }

    private static async ValueTask<ResumeAuthRequest> BindHttpRequestAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.HasJsonContentType())
        {
            throw new A2AHttpBindingException(
                Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));
        }

        ResumeAuthRequest request;
        try
        {
            request = await JsonSerializer.DeserializeAsync(
                context.Request.Body,
                InTaskAuthorizationJsonContext.Default.ResumeAuthRequest,
                cancellationToken).ConfigureAwait(false)
                ?? throw new JsonException("The resumeAuth request body is required.");
        }
        catch (JsonException exception)
        {
            throw new A2AException(
                "Invalid resumeAuth request body.",
                exception,
                A2AErrorCode.InvalidParams);
        }

        request.TaskId = context.Request.RouteValues["taskId"]?.ToString()
            ?? throw new A2AException(
                "The resumeAuth task ID route value is required.",
                A2AErrorCode.InvalidParams);
        return request;
    }

    private static void ValidateRequest(ResumeAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskId)
            || string.IsNullOrWhiteSpace(request.ContextId)
            || string.IsNullOrWhiteSpace(request.AuthorizationRequestId))
        {
            throw new A2AException(
                "resumeAuth requires taskId, contextId, and authorizationRequestId.",
                A2AErrorCode.InvalidParams);
        }
    }

    private static JsonTypeInfo<T> GetA2ATypeInfo<T>() =>
        (JsonTypeInfo<T>)A2AJsonUtilities.DefaultOptions.GetTypeInfo(typeof(T));
}
