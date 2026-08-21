// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Hosting.DirectLine.NamedPipes.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes
{
    /// <summary>
    /// Custom <see cref="HttpMessageHandler"/> that intercepts outgoing HTTP requests destined for
    /// the named pipe service URL (urn:botframework:namedpipe:*) and routes them
    /// back through the named pipe protocol instead of making an actual HTTP call.
    /// </summary>
    /// <remarks>
    /// This enables the Agents SDK's ConversationsRestClient to send reply activities
    /// back to DirectLineFlex through the named pipe, without any HTTP roundtrip.
    /// </remarks>
    /// <remarks>
    /// Initializes a new instance of the <see cref="NamedPipeMessageHandler"/> class.
    /// </remarks>
    /// <param name="logger">The logger instance.</param>
    internal sealed class NamedPipeMessageHandler(ILogger<NamedPipeMessageHandler> logger) : HttpMessageHandler
    {
        private const string StreamingAttachmentsOptionName = "Microsoft.Agents.Connector.StreamingAttachments";
        private static readonly HttpRequestOptionsKey<IList<(string ContentType, byte[] Body)>> StreamingAttachmentsOption = new(StreamingAttachmentsOptionName);

        private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private volatile NamedPipeProtocol _protocol;

        /// <summary>
        /// Set the active named pipe protocol instance. Called by the hosted service
        /// when a pipe connection is established.
        /// </summary>
        /// <param name="protocol">The protocol instance, or null when disconnected.</param>
        public void SetProtocol(NamedPipeProtocol protocol)
        {
            _protocol = protocol;
        }

        /// <summary>
        /// Send a request through the named pipe. Invoked by <see cref="PipeRoutingDelegatingHandler"/>.
        /// </summary>
        /// <param name="request">The HTTP request to route.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <returns>An HTTP response constructed from the pipe response.</returns>
        public Task<HttpResponseMessage> SendViaPipeAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => SendAsync(request, cancellationToken);

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri;
            if (uri == null)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (!PipeUriPredicate.IsNamedPipeUri(uri))
            {
                _logger.LogWarning("NamedPipeMessageHandler: Unexpected non-pipe URL: {Url}.", uri);
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }

            // Capture volatile field once to avoid race condition
            var protocol = _protocol;
            if (protocol == null)
            {
                _logger.LogError("NamedPipeMessageHandler: No active pipe connection for outbound request to {Path}.", uri.AbsolutePath);
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            // Extract the /v3/... path from the urn: URI. The Connector REST contract
            // requires a /v3/ prefixed path; anything else indicates a misrouted request
            // and must not be forwarded over the pipe.
            var fullUri = uri.AbsoluteUri;
            var pathStart = fullUri.IndexOf("/v3/", StringComparison.OrdinalIgnoreCase);
            if (pathStart < 0)
            {
                _logger.LogError("NamedPipeMessageHandler: Request URI does not contain a '/v3/' path segment: {Url}.", uri);
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            var path = fullUri[pathStart..];

            var verb = request.Method.Method;
            byte[] body = null;
            string contentType = null;
            if (request.Content != null)
            {
                body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                contentType = request.Content.Headers.ContentType?.ToString();
            }

            var attachments = ReadStreamingAttachments(request);

            _logger.LogDebug("NamedPipeMessageHandler: Routing {Verb} {Path} through pipe (BodyLen={Len}, ContentType={ContentType}, Attachments={AttachmentCount}).",
                verb, path, body?.Length ?? 0, contentType, attachments?.Count ?? 0);

            try
            {
                var response = await protocol.SendRequestAsync(verb, path, body, attachments, contentType, cancellationToken).ConfigureAwait(false);

                _logger.LogDebug("NamedPipeMessageHandler: Pipe response {StatusCode} for {Path}.",
                    response.StatusCode, path);

                var httpResponse = new HttpResponseMessage((HttpStatusCode)response.StatusCode);
                if (response.Body != null && response.Body.Length > 0)
                {
                    httpResponse.Content = new ByteArrayContent(response.Body);
                    var responseContentType = string.IsNullOrEmpty(response.ContentType) ? "application/json" : response.ContentType;
                    if (MediaTypeHeaderValue.TryParse(responseContentType, out var parsed))
                    {
                        httpResponse.Content.Headers.ContentType = parsed;
                    }
                    else
                    {
                        httpResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    }
                }

                return httpResponse;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("NamedPipeMessageHandler: Request cancelled for {Path}.", path);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NamedPipeMessageHandler: Failed to send through pipe for {Path}.", path);
                return new HttpResponseMessage(HttpStatusCode.BadGateway);
            }
        }

        private static List<NamedPipeAttachment> ReadStreamingAttachments(HttpRequestMessage request)
        {
            if (!request.Options.TryGetValue(StreamingAttachmentsOption, out var streamAttachments)
                || streamAttachments == null
                || streamAttachments.Count == 0)
            {
                return null;
            }

            var attachments = new List<NamedPipeAttachment>(streamAttachments.Count);
            foreach (var attachment in streamAttachments)
            {
                attachments.Add(new NamedPipeAttachment
                {
                    ContentType = string.IsNullOrEmpty(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
                    Body = attachment.Body ?? [],
                });
            }

            return attachments;
        }
    }
}
