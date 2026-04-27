// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A2A;

namespace A2ATCKAgent;

[A2ASkill("TCK", "tck")]
public class MyAgent(AgentApplicationOptions options) : AgentApplication(options)
{
}
