// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AAuthenticationSession
{
    public A2AAuthMode Mode { get; set; } = A2AAuthMode.None;

    public A2AAuthMode? ModeOverride { get; private set; }

    public void SetMode(A2AAuthMode mode)
    {
        ModeOverride = mode;
        Mode = mode;
    }

    public void SetAutomaticMode(A2AAuthMode mode)
    {
        Mode = mode;
    }

    public void ClearModeOverride()
    {
        ModeOverride = null;
    }
}
