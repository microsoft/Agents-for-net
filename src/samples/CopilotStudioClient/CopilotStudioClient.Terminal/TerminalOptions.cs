internal enum TerminalLayout
{
    Tabs,
    Split
}

internal sealed record TerminalOptions(
    TerminalLayout Layout,
    bool ShowHelp,
    TerminalConnectionOverrides? ConnectionOverrides = null)
{
    internal const string Usage =
        "Usage: CopilotStudioClient.Terminal.exe [options]\r\n"
        + "Options:\r\n"
        + "  --layout tabs|split\r\n"
        + "  --tenant-id <value>, -t <value>\r\n"
        + "  --app-client-id <value>, -c <value>\r\n"
        + "  --app-client-secret <value>, -k <value>\r\n"
        + "  --direct-connect-url <value>, -d <value>\r\n"
        + "  --environment-id <value>, -e <value>\r\n"
        + "  --schema-name <value>, -s <value>\r\n"
        + "  --use-s2s-connection true|false, -u true|false\r\n"
        + "  --help, -h\r\n"
        + "Exit codes: 0 success/help; 1 configuration/startup failure; 2 option/terminal usage error.";

    public static TerminalOptions Parse(string[] args)
    {
        TerminalLayout layout = TerminalLayout.Tabs;
        bool showHelp = false;
        TerminalConnectionOverrides connectionOverrides = new();

        for (int index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                case "--layout":
                    layout = ReadRequiredValue(args, ref index, "tabs or split")
                        .ToLowerInvariant() switch
                    {
                        "tabs" => TerminalLayout.Tabs,
                        "split" => TerminalLayout.Split,
                        _ => throw new TerminalOptionException(
                            "--layout requires tabs or split.")
                    };
                    break;
                case "--tenant-id":
                case "-t":
                    connectionOverrides.TenantId =
                        ReadRequiredValue(args, ref index, "a value");
                    break;
                case "--app-client-id":
                case "-c":
                    connectionOverrides.AppClientId =
                        ReadRequiredValue(args, ref index, "a value");
                    break;
                case "--app-client-secret":
                case "-k":
                    connectionOverrides.AppClientSecret =
                        ReadRequiredValue(args, ref index, "a value");
                    break;
                case "--direct-connect-url":
                case "-d":
                    connectionOverrides.DirectConnectUrl =
                        ReadRequiredValue(args, ref index, "a value");
                    break;
                case "--environment-id":
                case "-e":
                    connectionOverrides.EnvironmentId =
                        ReadRequiredValue(args, ref index, "a value");
                    break;
                case "--schema-name":
                case "-s":
                    connectionOverrides.SchemaName =
                        ReadRequiredValue(args, ref index, "a value");
                    break;
                case "--use-s2s-connection":
                case "-u":
                    string option = args[index];
                    string value = ReadRequiredValue(args, ref index, "true or false");
                    if (!bool.TryParse(value, out bool useS2SConnection))
                    {
                        throw new TerminalOptionException(
                            $"{option} requires true or false.");
                    }

                    connectionOverrides.UseS2SConnection = useS2SConnection;
                    break;
                default:
                    throw new TerminalOptionException("Unknown option.");
            }
        }

        return new TerminalOptions(layout, showHelp, connectionOverrides);
    }

    private static string ReadRequiredValue(
        string[] args,
        ref int index,
        string expectedValue)
    {
        string option = args[index];
        if (index + 1 >= args.Length || IsRecognizedOption(args[index + 1]))
        {
            throw new TerminalOptionException(
                $"{option} requires {expectedValue}.");
        }

        return args[++index];
    }

    private static bool IsRecognizedOption(string value)
    {
        return value is "--help"
            or "-h"
            or "--layout"
            or "--tenant-id"
            or "-t"
            or "--app-client-id"
            or "-c"
            or "--app-client-secret"
            or "-k"
            or "--direct-connect-url"
            or "-d"
            or "--environment-id"
            or "-e"
            or "--schema-name"
            or "-s"
            or "--use-s2s-connection"
            or "-u";
    }
}

internal sealed class TerminalConnectionOverrides
{
    private const string ConfigurationSectionName = "CopilotStudioClientSettings";

    internal string? TenantId { get; set; }

    internal string? AppClientId { get; set; }

    internal string? AppClientSecret { get; set; }

    internal string? DirectConnectUrl { get; set; }

    internal string? EnvironmentId { get; set; }

    internal string? SchemaName { get; set; }

    internal bool? UseS2SConnection { get; set; }

    internal IReadOnlyDictionary<string, string?> BuildConfigurationValues()
    {
        Dictionary<string, string?> values = [];

        AddIfSupplied(values, nameof(TenantId), TenantId);
        AddIfSupplied(values, nameof(AppClientId), AppClientId);
        AddIfSupplied(values, nameof(AppClientSecret), AppClientSecret);
        AddIfSupplied(values, nameof(DirectConnectUrl), DirectConnectUrl);
        AddIfSupplied(values, nameof(EnvironmentId), EnvironmentId);
        AddIfSupplied(values, nameof(SchemaName), SchemaName);
        if (UseS2SConnection.HasValue)
        {
            values[GetConfigurationKey(nameof(UseS2SConnection))] =
                UseS2SConnection.Value ? "true" : "false";
        }

        return values;
    }

    public override string ToString() => nameof(TerminalConnectionOverrides);

    private static void AddIfSupplied(
        IDictionary<string, string?> values,
        string propertyName,
        string? value)
    {
        if (value is not null)
        {
            values[GetConfigurationKey(propertyName)] = value;
        }
    }

    private static string GetConfigurationKey(string propertyName) =>
        $"{ConfigurationSectionName}:{propertyName}";
}

internal sealed class TerminalOptionException(string message) : Exception(message);
