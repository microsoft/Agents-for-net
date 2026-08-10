// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Hosting.DirectLine.NamedPipes.Tests
{
    internal static class TestTimeouts
    {
        // NamedPipes tests run alongside many other test assemblies in CI. Operations are
        // functionally immediate, but their continuations can be delayed under thread-pool load.
        internal static readonly TimeSpan Observe = TimeSpan.FromSeconds(30);
    }
}
