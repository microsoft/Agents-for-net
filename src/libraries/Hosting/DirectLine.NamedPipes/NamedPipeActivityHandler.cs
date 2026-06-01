// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Handles inbound named pipe requests by invoking the agent's turn pipeline directly,
    /// without any HTTP roundtrip. Activities arrive from the named pipe and are passed
    /// to <see cref="IChannelAdapter.ProcessActivityAsync"/>.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="NamedPipeActivityHandler"/> class.
    /// </remarks>
    /// <param name="services">The service provider for resolving scoped dependencies.</param>
    /// <param name="logger">The logger instance.</param>
    internal sealed class NamedPipeActivityHandler(IServiceProvider services, ILogger<NamedPipeActivityHandler> logger)
    {
        private readonly IServiceProvider _services = services ?? throw new ArgumentNullException(nameof(services));
        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        /// <summary>
        /// Process an inbound named pipe request. Returns a <see cref="NamedPipeResponse"/>.
        /// </summary>
        /// <param name="request">The inbound request.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>A response indicating the result of processing.</returns>
        public async Task<NamedPipeResponse> HandleAsync(NamedPipeRequest request, CancellationToken cancellationToken)
        {
            if (!string.Equals(request.Verb, "POST", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("NamedPipeActivityHandler: Unsupported verb {Verb} {Path}.", request.Verb, request.Path);
                return NamedPipeResponse.NotFound();
            }

            if (!string.Equals(request.Path, "/api/messages", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("NamedPipeActivityHandler: Unknown path {Path}, returning 404.", request.Path);
                return NamedPipeResponse.NotFound();
            }

            if (request.Body == null || request.Body.Length == 0)
            {
                _logger.LogWarning("NamedPipeActivityHandler: Empty body for POST /api/messages.");
                return new NamedPipeResponse { StatusCode = 400 };
            }

            _logger.LogDebug("NamedPipeActivityHandler: Processing activity (BodyLen={Len}, ContentType={ContentType}).", request.Body.Length, request.ContentType);

            // The Bot.Streaming wire format advertises per-stream content types. We accept JSON only
            // for the primary body; reject other content types with 415 so a misrouted upload doesn't
            // surface as a confusing JsonException downstream. Parse the value so we strictly match
            // the media type (rather than StartsWith, which would accept "application/jsonfoo").
            if (!string.IsNullOrEmpty(request.ContentType) && !IsJsonContentType(request.ContentType))
            {
                _logger.LogWarning("NamedPipeActivityHandler: Unsupported primary content type '{ContentType}' for POST /api/messages.", request.ContentType);
                return NamedPipeResponse.UnsupportedMediaType();
            }

            try
            {
                var activity = JsonSerializer.Deserialize<Activity>(
                    request.Body, ProtocolJsonSerializer.SerializationOptions);

                if (activity == null)
                {
                    _logger.LogWarning("NamedPipeActivityHandler: Failed to deserialize activity.");
                    return new NamedPipeResponse { StatusCode = 400 };
                }

                // Surface multi-stream attachments (Streams[1..N] in the protocol payload) onto
                // Activity.Attachments[] as raw bytes. DirectLineFlex sends an Activity plus its
                // attachment streams as a single multi-stream request — without this, every
                // non-JSON stream would be silently dropped.
                if (request.Attachments is { Count: > 0 })
                {
                    var merged = activity.Attachments != null
                        ? [.. activity.Attachments]
                        : new List<Attachment>(request.Attachments.Count);

                    foreach (var pipeAttachment in request.Attachments)
                    {
                        merged.Add(new Attachment
                        {
                            ContentType = string.IsNullOrEmpty(pipeAttachment.ContentType)
                                ? "application/octet-stream"
                                : pipeAttachment.ContentType,
                            Content = pipeAttachment.Body ?? [],
                        });
                    }

                    activity.Attachments = merged;
                }

                using var scope = _services.CreateScope();
                var adapter = scope.ServiceProvider.GetRequiredService<IChannelAdapter>();
                var agent = scope.ServiceProvider.GetRequiredService<IAgent>();

                // DirectLine handles external auth before forwarding over the trusted pipe.
                // Keep this anonymous so the SDK does not require configured token connections
                // for connector clients whose outbound calls are routed back through the pipe.
                var identity = new ClaimsIdentity();

                _logger.LogInformation("NamedPipeActivityHandler: Invoking agent for activity type={Type} id={Id}.",
                    activity.Type, activity.Id);

                await adapter.ProcessActivityAsync(identity, activity, agent.OnTurnAsync, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("NamedPipeActivityHandler: Agent completed successfully.");
                return NamedPipeResponse.Accepted();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeActivityHandler: Error processing activity.");
                return NamedPipeResponse.InternalServerError();
            }
        }

        /// <summary>
        /// Returns true when the supplied value parses as a JSON media type
        /// (e.g., <c>application/json</c> or <c>application/json; charset=utf-8</c>),
        /// false for any other type or unparseable input.
        /// Strict match — refuses near-misses like <c>application/jsonfoo</c>.
        /// </summary>
        private static bool IsJsonContentType(string contentType)
        {
            if (!MediaTypeHeaderValue.TryParse(contentType, out var parsed))
            {
                return false;
            }

            return string.Equals(parsed.MediaType, "application/json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
