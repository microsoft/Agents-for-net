// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using A2A;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class A2ARequestPlanner(
    AgentCard card,
    A2AAuthenticationSession authenticationSession,
    Action<A2AAgentCardAuthentication> configureAuthentication)
{
    public A2AAgentCardSkillSelection Plan(string input)
    {
        A2AAgentCardSkillSelection selection = A2AAgentCardSkillSelector.Select(card, input);
        if (selection.IsAmbiguous)
        {
            return selection;
        }

        A2AAgentCardAuthentication? authentication = authenticationSession.ModeOverride switch
        {
            A2AAuthMode.None => null,
            A2AAuthMode.Delegated or A2AAuthMode.App =>
                A2AAgentCardAuthentication.Select(card, authenticationSession.ModeOverride.Value),
            _ => A2AAgentCardAuthentication.Select(card, selection.Skill, modeOverride: null),
        };
        if (authentication is null)
        {
            authenticationSession.SetAutomaticMode(A2AAuthMode.None);
            return selection;
        }

        configureAuthentication(authentication);
        authenticationSession.SetAutomaticMode(authentication.Mode);
        return selection;
    }
}
