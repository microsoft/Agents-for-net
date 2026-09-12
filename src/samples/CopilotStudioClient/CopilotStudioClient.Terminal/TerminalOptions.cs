internal enum TerminalLayout
{
    Tabs,
    Split
}

internal sealed record TerminalOptions(TerminalLayout Layout, bool ShowHelp)
{
    internal const string Usage =
        "Usage: dotnet run --project CopilotStudioClient.Terminal.csproj -- [--layout tabs|split] [--help]\r\n"
        + "Exit codes: 0 success/help; 1 configuration/startup failure; 2 option/terminal usage error.";

    public static TerminalOptions Parse(string[] args)
    {
        TerminalLayout layout = TerminalLayout.Tabs;
        bool showHelp = false;

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--layout" when index + 1 < args.Length:
                    layout = args[++index].ToLowerInvariant() switch
                    {
                        "tabs" => TerminalLayout.Tabs,
                        "split" => TerminalLayout.Split,
                        string value => throw new TerminalOptionException(
                            $"Unsupported layout '{value}'. Expected tabs or split.")
                    };
                    break;
                case "--layout":
                    throw new TerminalOptionException("--layout requires tabs or split.");
                default:
                    throw new TerminalOptionException($"Unknown option '{args[index]}'.");
            }
        }

        return new TerminalOptions(layout, showHelp);
    }
}

internal sealed class TerminalOptionException(string message) : Exception(message);
