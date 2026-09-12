#nullable enable

using CopilotStudioClient.Terminal;
using Microsoft.Agents.CopilotStudio.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

return await TerminalProgram.RunAsync(
    args,
    Console.IsInputRedirected,
    Console.IsOutputRedirected,
    Console.Out,
    Console.Error);

internal static class TerminalProgram
{
    internal static async Task<int> RunAsync(
        string[] args,
        bool inputRedirected,
        bool outputRedirected,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        TerminalOptions options;
        try
        {
            options = TerminalOptions.Parse(args);
        }
        catch (TerminalOptionException exception)
        {
            await error.WriteLineAsync(exception.Message).ConfigureAwait(false);
            await error.WriteLineAsync(TerminalOptions.Usage).ConfigureAwait(false);
            return 2;
        }

        if (options.ShowHelp)
        {
            await output.WriteLineAsync(TerminalOptions.Usage).ConfigureAwait(false);
            return 0;
        }

        if (inputRedirected || outputRedirected)
        {
            await error.WriteLineAsync(
                "Copilot Studio Terminal Client requires an interactive terminal; input and output cannot be redirected.")
                .ConfigureAwait(false);
            return 2;
        }

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
            return await RunConfiguredAsync(
                options,
                builder,
                error,
                RunTerminalAsync,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync($"Startup failed: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    internal static async Task<int> RunConfiguredAsync(
        TerminalOptions options,
        HostApplicationBuilder builder,
        TextWriter error,
        Func<IServiceProvider, CancellationToken, Task> runTerminal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(runTerminal);

        try
        {
            SampleConnectionSettings settings = new(
                builder.Configuration.GetSection("CopilotStudioClientSettings"));

            builder.Services.AddHttpClient("mcs").ConfigurePrimaryHttpMessageHandler(() =>
                settings.UseS2SConnection
                    ? new AddTokenHandlerS2S(settings)
                    : new AddTokenHandler(settings));

            builder.Services
                .AddSingleton(options)
                .AddSingleton(settings)
                .AddTransient<CopilotClient>(services =>
                {
                    ILogger<CopilotClient> logger =
                        services.GetRequiredService<ILoggerFactory>().CreateLogger<CopilotClient>();
                    return new CopilotClient(
                        settings,
                        services.GetRequiredService<IHttpClientFactory>(),
                        logger,
                        "mcs");
                })
                .AddSingleton<ICopilotConversationClient, CopilotConversationClient>()
                .AddSingleton<ActivityJournal>()
                .AddSingleton<ActivityInterpreter>()
                .AddSingleton<ConversationSession>()
                .AddSingleton<TerminalChatApplication>()
                .AddSingleton<ITerminalView>(
                    services => services.GetRequiredService<TerminalChatApplication>())
                .AddSingleton<TerminalPresenter>();

            using IHost host = builder.Build();
            await runTerminal(host.Services, cancellationToken).ConfigureAwait(false);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception)
        {
            await error.WriteLineAsync($"Startup failed: {exception.Message}").ConfigureAwait(false);
            return 1;
        }
    }

    private static Task RunTerminalAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        TerminalChatApplication terminal =
            services.GetRequiredService<TerminalChatApplication>();
        TerminalPresenter presenter =
            services.GetRequiredService<TerminalPresenter>();
        return terminal.RunAsync(presenter, cancellationToken);
    }
}
