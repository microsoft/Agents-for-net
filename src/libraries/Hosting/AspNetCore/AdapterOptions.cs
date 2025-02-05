// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Hosting.AspNetCore
{
    public class AdapterOptions
    {
        public bool Async { get; set; } = true;
        public int ShutdownTimeoutSeconds { get; set; } = 60;
    }
}
