// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core
{
    /// <summary>
    /// Implemented by a Host to propagate request headers.
    /// </summary>
    public interface IHeaderPropagation
    {
        public Dictionary<string, string> Headers { get; }
        public string UserAgent { get; }
    }
}
