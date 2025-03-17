// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading;

namespace Microsoft.Agents.Core
{
    public class RequestContext
    {
        private readonly IHeaderPropagation _propagation;

        public RequestContext(IHeaderPropagation propagation)
        {
            _propagation = propagation;
            _asyncLocal.Value = this;
        }

        public static IHeaderPropagation GetHeaderPropagation()
        {
            return _asyncLocal.Value._propagation;
        }

        private static readonly AsyncLocal<RequestContext> _asyncLocal = new AsyncLocal<RequestContext>();
    }
}
