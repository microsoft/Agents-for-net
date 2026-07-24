// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Core.HeaderPropagation;

namespace Microsoft.Agents.Extensions.MSTeams.Integration;

[HeaderPropagation]

#if !NETSTANDARD
internal class HeaderPropagation : IHeaderPropagationAttribute
#else
internal class HeaderPropagation
#endif
{
    public static void LoadHeaders(HeaderPropagationEntryCollection collection)
    {
        collection.Append("User-Agent", $"agents-sdk-net-teams/{ThisAssembly.AssemblyFileVersion}");
    }
}
