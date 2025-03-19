// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.Agents.Core
{
    /// <summary>
    /// Interface to specify request headers that will propagate.  This is used by an
    /// implementation of <see cref="IHeaderPropagation"/>.
    /// </summary>
    public interface IHeaderPropagationFilter
    {
        public IList<string> Headers { get; }
        public string UserAgent { get; }
    }
}
