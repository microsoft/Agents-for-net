// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Samples.A2AClient;

internal sealed class Program
{
    private const string Usage =
        "Usage: A2AClient [--agent <url>] [--auth-mode none|delegated|app] [--history] "
        + "[--use-push-notifications] [--push-notification-receiver <url>] [--help]";

    private static async Task<int> Main(string[] args)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        try
        {
            StartupOptions startupOptions = ParseStartupOptions(args);
            if (startupOptions.ShowHelp)
            {
                Console.WriteLine(Usage);
                return 0;
            }

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables("A2ACLIENT_")
                .AddUserSecrets<Program>(optional: true)
                .Build();

            A2AClientOptions options = A2AClientOptions.FromConfiguration(configuration, startupOptions);
            var authenticationSession = new A2AAuthenticationSession
            {
                Mode = startupOptions.AuthMode,
            };
            var accessTokenProvider = new A2AAccessTokenProvider(new MsalTokenClient(options.Authentication));
            using var httpClient = new HttpClient(
                new AuthenticatedA2AHttpHandler(authenticationSession, accessTokenProvider, options.AgentUrl));

            var resolver = new A2ACardResolver(options.AgentUrl, httpClient);
            AgentCard card = await resolver.GetAgentCardAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            IA2AClient client = CreateClient(card, httpClient, options.AgentUrl);

            var console = new A2AConsole(
                client,
                card,
                authenticationSession,
                Console.In,
                Console.Out,
                options.ShowHistory,
                options.UsePushNotifications,
                options.PushNotificationReceiver);

            return await console.RunAsync(cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(Usage);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    internal static StartupOptions ParseStartupOptions(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Uri? agentUrl = null;
        Uri? pushNotificationReceiver = null;
        A2AAuthMode authMode = A2AAuthMode.None;
        bool showHistory = false;
        bool usePushNotifications = false;
        bool showHelp = false;

        for (int index = 0; index < args.Length; index++)
        {
            string argument = args[index];
            if (string.Equals(argument, "--help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "-h", StringComparison.OrdinalIgnoreCase)
                || string.Equals(argument, "/?", StringComparison.OrdinalIgnoreCase))
            {
                showHelp = true;
            }
            else if (string.Equals(argument, "--history", StringComparison.OrdinalIgnoreCase))
            {
                showHistory = true;
            }
            else if (string.Equals(argument, "--use-push-notifications", StringComparison.OrdinalIgnoreCase))
            {
                usePushNotifications = true;
            }
            else if (string.Equals(argument, "--agent", StringComparison.OrdinalIgnoreCase))
            {
                agentUrl = ReadAbsoluteUri(args, ref index, "--agent");
            }
            else if (string.Equals(argument, "--push-notification-receiver", StringComparison.OrdinalIgnoreCase))
            {
                pushNotificationReceiver = ReadAbsoluteUri(args, ref index, "--push-notification-receiver");
            }
            else if (string.Equals(argument, "--auth-mode", StringComparison.OrdinalIgnoreCase))
            {
                string mode = ReadValue(args, ref index, "--auth-mode");
                if (!Enum.TryParse(mode, ignoreCase: true, out authMode) || !Enum.IsDefined(authMode))
                {
                    throw new ArgumentException(
                        $"Unsupported auth mode '{mode}'. Expected none, delegated, or app.",
                        nameof(args));
                }
            }
            else
            {
                throw new ArgumentException($"Unknown option '{argument}'.", nameof(args));
            }
        }

        return new StartupOptions
        {
            AgentUrl = agentUrl,
            AuthMode = authMode,
            ShowHistory = showHistory,
            UsePushNotifications = usePushNotifications,
            PushNotificationReceiver = pushNotificationReceiver,
            ShowHelp = showHelp,
        };
    }

    /// <summary>
    /// Selects an advertised interface to talk to.
    /// </summary>
    /// <remarks>
    /// Only interfaces on the configured agent origin are accepted. The Agent Card is data returned by the
    /// agent, so an interface URL pointing somewhere else must fail here — before the shared authenticated
    /// <see cref="HttpClient"/> could attach the Agent API access token to it.
    /// </remarks>
    internal static IA2AClient CreateClient(AgentCard card, HttpClient httpClient, Uri agentUrl)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(httpClient);

        Uri agentOrigin = A2AAgentOrigin.FromAgentUrl(agentUrl);

        List<AgentInterface> supported = (card.SupportedInterfaces ?? [])
            .Where(item => IsSupportedBinding(item.ProtocolBinding))
            .ToList();

        if (supported.Count == 0)
        {
            throw new InvalidOperationException(
                "The Agent Card does not advertise a supported JSON-RPC or HTTP+JSON interface.");
        }

        AgentInterface? interfaceDefinition = null;
        Uri? interfaceUri = null;
        foreach (AgentInterface candidate in supported)
        {
            if (Uri.TryCreate(candidate.Url, UriKind.Absolute, out Uri? candidateUri)
                && A2AAgentOrigin.IsSameOrigin(agentOrigin, candidateUri))
            {
                interfaceDefinition = candidate;
                interfaceUri = candidateUri;
                break;
            }
        }

        if (interfaceDefinition is null || interfaceUri is null)
        {
            throw new InvalidOperationException(
                $"The Agent Card advertises no supported interface on the configured agent origin '{agentOrigin.GetLeftPart(UriPartial.Authority)}'. "
                + "Refusing to use an interface hosted elsewhere.");
        }

        return string.Equals(interfaceDefinition.ProtocolBinding, ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase)
            ? new global::A2A.A2AClient(interfaceUri, httpClient)
            : new A2AHttpJsonClient(interfaceUri, httpClient);
    }

    private static bool IsSupportedBinding(string? protocolBinding)
        => string.Equals(protocolBinding, ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocolBinding, ProtocolBindingNames.HttpJson, StringComparison.OrdinalIgnoreCase);

    private static Uri ReadAbsoluteUri(string[] args, ref int index, string optionName)
    {
        string value = ReadValue(args, ref index, optionName);
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException($"Option {optionName} requires an absolute URI.", nameof(args));
        }

        return uri;
    }

    private static string ReadValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for {optionName}.", nameof(args));
        }

        return args[++index];
    }
}
