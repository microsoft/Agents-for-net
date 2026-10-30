// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;

namespace Microsoft.Agents.Hosting.AgentRunner;

internal sealed record AgentRunnerArgs(int Port, string AgentAssemblyPath)
{
    internal static AgentRunnerArgs? Parse(string[] args, out IReadOnlyList<string> errors)
    {
        var port = new Option<int>("--port") { Required = true };
        var assembly = new Option<string>("--agent-assembly") { Required = true };
        var command = new RootCommand("Host an Agents SDK registration-contract assembly") { port, assembly };
        var result = command.Parse(args);
        var validationErrors = result.Errors.Select(error => error.Message).ToList();
        if (result.Errors.Count == 0)
        {
            if (result.GetValue(port) is < 1 or > 65535)
            {
                validationErrors.Add("--port must be between 1 and 65535.");
            }

            if (string.IsNullOrWhiteSpace(result.GetValue(assembly)))
            {
                validationErrors.Add("--agent-assembly must specify an assembly path.");
            }
        }

        errors = validationErrors;
        return validationErrors.Count == 0
            ? new AgentRunnerArgs(result.GetValue(port), result.GetValue(assembly)!)
            : null;
    }
}
