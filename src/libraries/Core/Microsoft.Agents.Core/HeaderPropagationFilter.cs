// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core
{
    /// <summary>
    /// Simple <see cref="IHeaderPropagationFilter"/> that contains a list of request headers to propagate to
    /// outgoing requests.
    /// </summary>
    /// <param name="headers"></param>
    /// <param name="userAgentAddition"></param>
    public class HeaderPropagationFilter(IList<string> headers = null, string userAgentAddition = null) : IHeaderPropagationFilter
    {
        public IList<string> Headers => headers ?? [];

        public string UserAgent => userAgentAddition;
    }
}
