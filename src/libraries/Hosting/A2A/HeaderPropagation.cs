// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.HeaderPropagation;
using System.Reflection;

namespace Microsoft.Agents.Hosting.A2A;

[HeaderPropagation]

#if !NETSTANDARD
internal class HeaderPropagation : IHeaderPropagationAttribute
#else
internal class HeaderPropagation
#endif
{
    public static void LoadHeaders(HeaderPropagationEntryCollection collection)
    {
        collection.Append("User-Agent", $"a2a/{Assembly.GetExecutingAssembly().GetName().Version}");
    }
}
