// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

#nullable enable annotations

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;

namespace Microsoft.Agents.Builder.Testing.Http
{
    /// <summary>An immutable, sanitized snapshot of one intercepted transport attempt.</summary>
    public sealed class HttpExchange
    {
        internal HttpExchange(long id, string clientName, HttpMethod method, Uri requestUri,
            IReadOnlyDictionary<string, IReadOnlyList<string>> headers, string body,
            int expectationId, int sequenceStep, HttpStatusCode? statusCode, string exceptionType)
        {
            Id = id;
            ClientName = clientName;
            Method = method;
            RequestUri = requestUri;
            Headers = headers;
            Body = body;
            ExpectationId = expectationId;
            SequenceStep = sequenceStep;
            StatusCode = statusCode;
            ExceptionType = exceptionType;
        }

        /// <summary>Gets the monotonically increasing attempt ID.</summary>
        public long Id { get; }

        /// <summary>Gets the factory client name, or null when no name was supplied.</summary>
        public string? ClientName { get; }

        /// <summary>Gets the request method.</summary>
        public HttpMethod Method { get; }

        /// <summary>Gets the absolute URI with user information, fragments, and credential query values removed or redacted.</summary>
        public Uri? RequestUri { get; }

        /// <summary>Gets immutable request and content headers, with credential values redacted.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Headers { get; }

        /// <summary>Gets the optionally captured body, or null when body capture is disabled.</summary>
        public string? Body { get; }

        /// <summary>Gets the matched expectation ID, or zero when no step was reserved.</summary>
        public int ExpectationId { get; }

        /// <summary>Gets the one-based reserved step, or zero when no step was reserved.</summary>
        public int SequenceStep { get; }

        /// <summary>Gets the dispatched status, or null for a pending or failed dispatch.</summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>Gets the exception type name without its message, or null when no exception was dispatched.</summary>
        public string? ExceptionType { get; }
    }
}
