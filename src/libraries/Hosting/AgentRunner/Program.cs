// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Microsoft.Agents.Hosting.AgentRunner;

var parsed = AgentRunnerArgs.Parse(args, out var errors);
if (parsed is null)
{
    foreach (var error in errors)
    {
        Console.Error.WriteLine(error);
    }

    return 1;
}

await using var app = AgentRunnerHost.BuildApp(parsed);
await app.RunAsync().ConfigureAwait(false);
return 0;
