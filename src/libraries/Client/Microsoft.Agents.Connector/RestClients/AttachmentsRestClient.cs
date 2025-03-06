// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable disable

using Microsoft.Agents.Connector.Types;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Connector.RestClients
{
    internal class AttachmentsRestClient(IRestTransport transport) : IAttachments
    {
        private readonly IRestTransport _transport = transport ?? throw new ArgumentNullException(nameof(_transport));

        /// <summary>
        /// Get the URI of an attachment view.
        /// </summary>
        /// <param name="attachmentId">id of the attachment.</param>
        /// <param name="viewId">default is "original".</param>
        /// <returns>uri.</returns>
#pragma warning disable CA1055 // Uri return values should not be strings (we can't change this without breaking binary compat)
        public string GetAttachmentUri(string attachmentId, string viewId = "original")
#pragma warning restore CA1055 // Uri return values should not be strings
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(attachmentId);

            // Construct URL
            var baseUrl = _transport.Endpoint.ToString();
            var url = new Uri(new Uri(baseUrl + (baseUrl.EndsWith("/", StringComparison.OrdinalIgnoreCase) ? string.Empty : "/", StringComparison.OrdinalIgnoreCase)), "v3/attachments/{attachmentId}/views/{viewId}").ToString();
            url = url.Replace("{attachmentId}", Uri.EscapeDataString(attachmentId));
            url = url.Replace("{viewId}", Uri.EscapeDataString(viewId));
            return url;
        }

        internal HttpRequestMessage CreateGetAttachmentInfoRequest(string attachmentId)
        {
            var request = new HttpRequestMessage();
            request.Method = HttpMethod.Get;
            request.RequestUri = new Uri(_transport.Endpoint, $"v3/attachments/{attachmentId}");
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
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
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
            request.RequestUri = new Uri(_transport.Endpoint, $"v3/attachments/{attachmentId}/views/{viewId}");
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
            using var httpClient = await _transport.GetHttpClientAsync().ConfigureAwait(false);
            using var httpResponse = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
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
