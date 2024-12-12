// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.Protocols.Primitives;
using Microsoft.Agents.Protocols.Serializer;

namespace Microsoft.Agents.Protocols.Connector
{
    /// <summary> The ClientDiagnostics is used to provide tracing support for the client library. </summary>
    /// <summary> Initializes a new instance of AttachmentsRestClient. </summary>
    /// <param name="client">The HttpClient</param>
    /// <param name="endpoint"> server parameter. </param>
    internal class AttachmentsRestClient(HttpClient client, Uri endpoint) : IAttachments
    {
        private readonly HttpClient _httpClient = client ?? throw new ArgumentNullException(nameof(client));
        private readonly Uri _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));

        internal HttpRequestMessage CreateGetAttachmentInfoRequest(string attachmentId)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri(endpoint, $"v3/attachments/{attachmentId}");
            request.Headers.Add("Accept", "application/json");
            return request;
        }

        /// <summary> GetAttachmentInfo. </summary>
        /// <param name="attachmentId"> attachment id. </param>
        /// <param name="cancellationToken"> The cancellation token to use. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="attachmentId"/> is null. </exception>
        /// <remarks> Get AttachmentInfo structure describing the attachment views. </remarks>
        public async Task<AttachmentInfo> GetAttachmentInfoAsync(string attachmentId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(attachmentId))
            {
                throw new ArgumentNullException(nameof(attachmentId));
            }

            using var message = CreateGetAttachmentInfoRequest(attachmentId);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int) httpResponse.StatusCode)
            {
                case 200:
                    {
                        return ProtocolJsonSerializer.ToObject<AttachmentInfo>(httpResponse.Content.ReadAsStream(cancellationToken));
                    }
                default:
                    {
                        var ex = new ErrorResponseException($"GetAttachmentInfo operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }

        internal HttpRequestMessage CreateGetAttachmentRequest(string attachmentId, string viewId)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri(endpoint, $"v3/attachments/{attachmentId}/views/{viewId}");
            request.Headers.Add("Accept", "application/octet-stream, application/json");
            return request;
        }

        /// <summary> GetAttachment. </summary>
        /// <param name="attachmentId"> attachment id. </param>
        /// <param name="viewId"> View id from attachmentInfo. </param>
        /// <param name="cancellationToken"> The cancellation token to use. </param>
        /// <exception cref="ArgumentNullException"> <paramref name="attachmentId"/> or <paramref name="viewId"/> is null. </exception>
        /// <remarks> Get the named view as binary content. </remarks>
        public async Task<Stream> GetAttachmentAsync(string attachmentId, string viewId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(attachmentId))
            {
                throw new ArgumentNullException(nameof(attachmentId));
            }
            if (string.IsNullOrEmpty(viewId))
            {
                throw new ArgumentNullException(nameof(viewId));
            }

            using var message = CreateGetAttachmentRequest(attachmentId, viewId);
            using var httpResponse = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            switch ((int)httpResponse.StatusCode)
            {
                case 200:
                    {
                        var memoryStream = new MemoryStream();
                        httpResponse.Content.ReadAsStream(cancellationToken).CopyTo(memoryStream);
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        return memoryStream;
                    }
                case 301:
                case 302:
                    return null;
                default:
                    {
                        var ex = new ErrorResponseException($"GetAttachment operation returned an invalid status code '{httpResponse.StatusCode}'");
                        try
                        {
                            ErrorResponse errorBody = ProtocolJsonSerializer.ToObject<ErrorResponse>(httpResponse.Content.ReadAsStream(cancellationToken));
                            if (errorBody != null)
                            {
                                ex.Body = errorBody;
                            }
                        }
                        catch (System.Text.Json.JsonException)
                        {
                            // Ignore the exception
                        }
                        throw ex;
                    }
            }
        }
    }
}
