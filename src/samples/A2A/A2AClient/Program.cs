// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.Agents.Samples.A2AClient;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0)
            {
                IReadOnlyDictionary<string, string?> options = ParseStartupOptions(args);
                if (options.ContainsKey("help"))
                {
                    Console.WriteLine("Usage: A2AClient [--auth-mode none|delegated|app] [--help]");
                    return 0;
                }

                if (options.TryGetValue("auth-mode", out string? rawMode) && rawMode is not null
                    && !Enum.TryParse(rawMode, ignoreCase: true, out A2AAuthMode _))
                {
                    Console.Error.WriteLine($"Unsupported auth mode '{rawMode}'. Expected none, delegated, or app.");
                    return 1;
                }
            }
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Usage: A2AClient [--auth-mode none|delegated|app] [--help]");
            return 1;
        }

        Console.WriteLine("A2AClient authentication core is ready. Interactive mode is added in Task 3.");
        return 0;
    }

    private static IReadOnlyDictionary<string, string?> ParseStartupOptions(string[] args)
    {
        var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "/?", StringComparison.OrdinalIgnoreCase))
            {
                options["help"] = null;
                continue;
            }

            if (string.Equals(argument, "--auth-mode", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("Missing value for --auth-mode.", nameof(args));
                }

                options["auth-mode"] = args[++index];
                continue;
            }

            throw new ArgumentException($"Unknown option '{argument}'.", nameof(args));
        }

        return options;
    }
}
