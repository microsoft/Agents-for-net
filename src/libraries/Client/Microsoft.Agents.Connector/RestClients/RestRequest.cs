// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Models;
using Microsoft.Agents.Core.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace Microsoft.Agents.Connector.RestClients
{
    /// <summary>
    /// Fluent builder for <see cref="System.Net.Http.HttpRequestMessage"/>.
    /// Replaces the Create*Request() helper methods scattered across REST clients.
    /// </summary>
    internal sealed class RestRequest
    {
        internal const string StreamingAttachmentsOptionName = "Microsoft.Agents.Connector.StreamingAttachments";
#if !NETSTANDARD2_0
        internal static readonly HttpRequestOptionsKey<IList<(string ContentType, byte[] Body)>> StreamingAttachmentsOption = new(StreamingAttachmentsOptionName);
#endif

        private readonly HttpMethod _method;
        private readonly string _path;
        private object _body;
        private readonly List<(string name, string value, bool escape)> _queryParams = [];

        private RestRequest(HttpMethod method, string path)
        {
            _method = method;
            _path = path;
        }

        public static RestRequest Get(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Get, path);
        }

        public static RestRequest Post(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Post, path);
        }

        public static RestRequest Put(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Put, path);
        }

        public static RestRequest Delete(string path)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(path, nameof(path));
            return new(HttpMethod.Delete, path);
        }

        /// <summary>Sets the JSON request body.</summary>
        public RestRequest WithBody<T>(T body)
        {
            _body = body;
            return this;
        }

        /// <summary>Appends a query parameter. Null values are omitted.</summary>
        public RestRequest WithQuery(string name, string value, bool escape = true)
        {
            AssertionHelpers.ThrowIfNullOrWhiteSpace(name, nameof(name));
            _queryParams.Add((name, value, escape));
            return this;
        }

        /// <summary>
        /// Builds the <see cref="System.Net.Http.HttpRequestMessage"/>.
        /// The caller is responsible for disposing the returned message.
        /// - URI is resolved relative to <paramref name="baseUri"/> (trailing slash is ensured).
        /// - Accept: application/json is added.
        /// - Body (if set) is serialized as application/json using ProtocolJsonSerializer.
        /// Auth headers are NOT added here; those come from IRestTransport.GetHttpClientAsync().
        /// </summary>
        public HttpRequestMessage Build(Uri baseUri)
        {
            AssertionHelpers.ThrowIfNull(baseUri, nameof(baseUri));

            var uri = new Uri(baseUri.EnsureTrailingSlash(), _path);
            foreach (var (name, value, escape) in _queryParams)
            {
                uri = uri.AppendQuery(name, value, escape);
            }

            var message = new HttpRequestMessage(_method, uri);
            message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (_body != null)
            {
#if !NETSTANDARD2_0
                var json = SerializeBody(_body, out var streamAttachments);
#else
                var json = ProtocolJsonSerializer.ToJson(_body);
#endif
                message.Content = new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

#if !NETSTANDARD2_0
                if (streamAttachments != null && streamAttachments.Count > 0)
                {
                    message.Options.Set(StreamingAttachmentsOption, streamAttachments);
                }
#endif
            }

            return message;
        }

        private static string SerializeBody(object body, out IList<(string ContentType, byte[] Body)> streamAttachments)
        {
            streamAttachments = null;
            if (body is not Activity activity || activity.Attachments == null)
            {
                return ProtocolJsonSerializer.ToJson(body);
            }

            var streamingAttachments = activity.Attachments
                .Where(a => IsStreamingAttachmentContent(a?.Content))
                .ToList();

            if (streamingAttachments.Count == 0)
            {
                return ProtocolJsonSerializer.ToJson(body);
            }

            var originalAttachments = activity.Attachments;
            activity.Attachments = originalAttachments
                .Where(a => !IsStreamingAttachmentContent(a?.Content))
                .ToList();

            try
            {
                var json = ProtocolJsonSerializer.ToJson(body);
                streamAttachments = streamingAttachments
                    .Select(CreateBufferedAttachment)
                    .ToList();
                return json;
            }
            finally
            {
                activity.Attachments = originalAttachments;
            }
        }

        private static (string ContentType, byte[] Body) CreateBufferedAttachment(Attachment attachment)
        {
            if (attachment.Content is byte[] bytes)
            {
                return (
                    string.IsNullOrEmpty(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
                    bytes.ToArray());
            }

            var stream = (Stream)attachment.Content;
            var originalPosition = stream.CanSeek ? stream.Position : (long?)null;

            try
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return (
                    string.IsNullOrEmpty(attachment.ContentType) ? "application/octet-stream" : attachment.ContentType,
                    ms.ToArray());
            }
            finally
            {
                if (originalPosition.HasValue)
                {
                    stream.Position = originalPosition.Value;
                }
            }
        }

        private static bool IsStreamingAttachmentContent(object content)
        {
            return content is Stream || content is byte[];
        }
    }
}
