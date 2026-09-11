TerminalOptions options = TerminalOptions.Parse(args);

if (options.ShowHelp)
{
    Console.WriteLine(TerminalOptions.Usage);
}
