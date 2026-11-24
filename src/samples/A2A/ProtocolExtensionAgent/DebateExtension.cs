// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.AspNetCore.Http;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace ProtocolExtensionAgent;

public sealed class DebateExtension : IA2AProtocolExtensionContributor
{
    public const string ExtensionUri = "https://example.org/extensions/debate/v1";

    public void Register(A2AProtocolExtensionBuilder builder)
    {
        var extension = builder.RegisterExtension(new AgentExtension
        {
            Uri = ExtensionUri,
            Description = "A contrived debate mode and continuation operation.",
            Required = false,
        });
        var resume = builder.Map<ResumeRequest, ResumeResult>(
            extension,
            new A2AOperationId("https://example.org/operations#resume-debate"),
            HandleResumeAsync,
            DebateJsonContext.Default.ResumeRequest,
            DebateJsonContext.Default.ResumeResult,
            ValidateRequest);
        builder.JsonRpc.Map("debate/resume", resume);
        builder.Http.Map(HttpMethods.Post, "/debate:resume", resume, ReadResumeRequestAsync);
    }

    private static ValueTask<ResumeResult> HandleResumeAsync(
        ResumeRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new ResumeResult(
            $"Counterpoint on {request.Topic}: what if the opposite of '{request.Argument}' were true?"));
    }

    private static async ValueTask<ResumeRequest> ReadResumeRequestAsync(
        HttpContext context, CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync(
                context.Request.Body, DebateJsonContext.Default.ResumeRequest, cancellationToken)
                ?? throw new JsonException();
        }
        catch (JsonException exception)
        {
            throw new A2AException("A JSON body with topic and argument is required.", exception, A2AErrorCode.InvalidParams);
        }
    }

    private static void ValidateRequest(ResumeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Topic) || string.IsNullOrWhiteSpace(request.Argument))
        {
            throw new A2AException("Both topic and argument are required.", A2AErrorCode.InvalidParams);
        }
    }
}

public sealed record ResumeRequest(string Topic, string Argument);
public sealed record ResumeResult(string Reply);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ResumeRequest))]
[JsonSerializable(typeof(ResumeResult))]
internal partial class DebateJsonContext : JsonSerializerContext;
