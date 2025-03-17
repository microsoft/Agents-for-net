// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace Microsoft.Agents.Hosting.AspNetCore
{
    internal class AspNetHeaderPropagation : IHeaderPropagation
    {
        private const string CorrelationId = "x-ms-conversation-id";

        private readonly Dictionary<string, string> _filteredHeaders = [];
        private readonly IHeaderPropagationFilter _inner;

        public AspNetHeaderPropagation(IHeaderDictionary headers, IHeaderPropagationFilter propagateFilter)
        {
            _inner = propagateFilter;

            if (_inner.Headers != null)
            {
                foreach (var header in _inner.Headers)
                {
                    if (headers.TryGetValue(header, out var value))
                    {
                        _filteredHeaders[header] = value.ToString();
                    }
                }
            }

            // always add the correlation id.
            if (!_filteredHeaders.ContainsKey(CorrelationId))
            {
                if (headers.TryGetValue(CorrelationId, out var value))
                {
                    _filteredHeaders.Add(CorrelationId, value.ToString());
                }
            }
        }

        public Dictionary<string, string> Headers => _filteredHeaders;

        public string UserAgent => _inner.UserAgent;
    }
}
