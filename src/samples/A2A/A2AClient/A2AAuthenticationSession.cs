// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2AAuthenticationSession
{
    public A2AAuthMode Mode { get; set; } = A2AAuthMode.None;

    public A2AAuthMode? ModeOverride { get; private set; }

    public A2AAgentCardAuthentication? SelectedAuthentication { get; private set; }

    public void SetMode(A2AAuthMode mode)
    {
        ModeOverride = mode;
        if (Mode != mode)
        {
            SelectedAuthentication = null;
        }

        Mode = mode;
    }

    public void SetAutomaticAuthentication(A2AAgentCardAuthentication? authentication)
    {
        SelectedAuthentication = authentication;
        Mode = authentication?.Mode ?? A2AAuthMode.None;
    }

    public void ClearModeOverride()
    {
        ModeOverride = null;
    }
}
