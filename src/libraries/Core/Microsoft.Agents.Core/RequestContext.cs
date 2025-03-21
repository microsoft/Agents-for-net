// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.Agents.Core
{
    /// <summary>
    /// Provides AsyncLocal request context.
    /// </summary>
    public class RequestContext
    {
        private readonly IHeaderPropagation _propagation;

        public RequestContext(IHeaderPropagation propagation)
        {
            _propagation = propagation;
            _asyncLocal.Value = this;
        }

        /// <summary>
        /// Gets list of headers to propagate to outgoing requests.
        /// </summary>
        /// <returns></returns>
        public static IHeaderPropagation GetHeaderPropagation()
        {
            return _asyncLocal?.Value?._propagation;
        }

        private static readonly AsyncLocal<RequestContext> _asyncLocal = new();
    }
}
