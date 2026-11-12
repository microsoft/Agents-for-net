// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using A2AProtocol = A2A;
using Microsoft.Agents.Samples.A2ACli.A2A;
using Microsoft.Agents.Samples.A2ACli.Configuration;
using Microsoft.Agents.Samples.A2ACli.OAuth.Configuration;
using Microsoft.Agents.Samples.A2ACli.OAuth.Discovery;
using Microsoft.Agents.Samples.A2ACli.OAuth.Providers;
using Microsoft.Agents.Samples.A2ACli.OAuth.Registration;
using Microsoft.Agents.Samples.A2ACli.OAuth.Tokens;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Agents.Samples.A2ACli;

internal sealed class Program
{
    private const string Usage =
        "Usage: A2ACli [--agent <url>] [--auth-mode none|delegated|app] [--history] "
        + "[--transport auto|jsonrpc|http-json|grpc] "
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
                .AddEnvironmentVariables("A2ACLI_")
                .AddUserSecrets<Program>(optional: true)
                .Build();

            A2ACliOptions options = A2ACliOptions.FromConfiguration(configuration, startupOptions);
            var authenticationSession = new A2AAuthenticationSession();
            using var oauthHttpClient = new HttpClient(CreateNoRedirectHttpHandler());
            using var metadataHttpClient = new HttpClient(CreateNoRedirectHttpHandler());
            using var registrationHttpClient = new HttpClient(CreateNoRedirectHttpHandler());
            var registrationStore = new InMemoryOAuthClientRegistrationStore();
            var approval = new ConsoleOAuthProviderApproval();
            var metadataClient = new OAuthAuthorizationServerMetadataClient(metadataHttpClient);
            var registrationClient = new DynamicClientRegistrationClient(registrationHttpClient);
            var oauthTokenClient = new OAuthTokenClient(
                new OAuthDeviceCodeTokenClient(oauthHttpClient, Console.Out),
                new OAuthAuthorizationCodeTokenClient(
                    oauthHttpClient,
                    new LoopbackOAuthAuthorizationCodeReceiver()),
                new OAuthClientCredentialsTokenClient(oauthHttpClient),
                new OAuthTokenEndpointClient(oauthHttpClient));
            var accessTokenProvider = new A2AAccessTokenProvider(
                new OAuthCredentialProviderResolver(
                    CreateCredentialProviders(
                        options.Authentication,
                        metadataClient,
                        registrationClient,
                        registrationStore,
                        approval)),
                oauthTokenClient,
                A2AAgentOrigin.FromAgentUrl(options.AgentUrl));
            using var httpClient = CreateAgentHttpClient(
                new AuthenticatedA2AHttpHandler(authenticationSession, accessTokenProvider, options.AgentUrl),
                options.AgentUrl,
                options.Transport);
            await using var notificationReceiver = options.UsePushNotifications
                ? await PushNotificationReceiver.StartAsync(
                    options.PushNotificationReceiver,
                    notification => Console.WriteLine(
                        $"\nPush notification => {System.Text.Json.JsonSerializer.Serialize(
                            notification,
                            A2AProtocol.A2AJsonUtilities.DefaultOptions)}"),
                    cancellationTokenSource.Token).ConfigureAwait(false)
                : null;

            if (notificationReceiver is not null)
            {
                Console.WriteLine($"Push notification receiver listening at {notificationReceiver.NotificationUri}");
            }

            var resolver = new A2AProtocol.A2ACardResolver(options.AgentUrl, httpClient);
            A2AProtocol.AgentCard card = await resolver.GetAgentCardAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            if (startupOptions.AuthMode is A2AAuthMode authMode)
            {
                authenticationSession.SetMode(authMode);
            }
            using A2AClientConnection connection = CreateClient(card, httpClient, options.AgentUrl, options.Transport);
            A2AInTaskAuthorizationClient inTaskAuthorizationClient =
                A2AInTaskAuthorizationClient.Create(connection, httpClient, accessTokenProvider);

            var console = new A2AConsole(
                connection.Client,
                card,
                authenticationSession,
                Console.In,
                Console.Out,
                options.ShowHistory,
                notificationReceiver?.NotificationUri,
                inTaskAuthorizationClient);

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
        A2AAuthMode? authMode = null;
        A2ATransport? transport = null;
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
                if (!Enum.TryParse(mode, ignoreCase: true, out A2AAuthMode parsedMode)
                    || !Enum.IsDefined(parsedMode))
                {
                    throw new ArgumentException(
                        $"Unsupported auth mode '{mode}'. Expected none, delegated, or app.",
                        nameof(args));
                }

                authMode = parsedMode;
            }
            else if (string.Equals(argument, "--transport", StringComparison.OrdinalIgnoreCase))
            {
                transport = A2ACliOptions.ParseTransport(ReadValue(args, ref index, "--transport"));
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
            Transport = transport,
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
    /// <see cref="System.Net.Http.HttpClient"/> could attach the Agent API access token to it.
    /// </remarks>
    internal static A2AClientConnection CreateClient(
        A2AProtocol.AgentCard card,
        HttpClient httpClient,
        Uri agentUrl,
        A2ATransport transport = A2ATransport.Auto)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(httpClient);

        Uri agentOrigin = A2AAgentOrigin.FromAgentUrl(agentUrl);

        List<A2AProtocol.AgentInterface> supported = (card.SupportedInterfaces ?? [])
            .Where(item => IsSupportedBinding(item.ProtocolBinding) && MatchesTransport(item.ProtocolBinding, transport))
            .ToList();

        if (supported.Count == 0)
        {
            throw new InvalidOperationException(
                transport == A2ATransport.Auto
                    ? "The Agent Card does not advertise a supported JSON-RPC, HTTP+JSON, or GRPC interface."
                    : $"The Agent Card does not advertise the requested {transport} transport.");
        }

        bool rejectedGrpcPrefix = false;
        foreach (A2AProtocol.AgentInterface candidate in supported)
        {
            bool isGrpc = string.Equals(candidate.ProtocolBinding, A2AProtocol.ProtocolBindingNames.Grpc, StringComparison.OrdinalIgnoreCase);
            string? target = candidate.Url;
            if (isGrpc && !string.IsNullOrWhiteSpace(target) && !target.Contains("://", StringComparison.Ordinal))
            {
                target = $"https://{target}";
            }

            if (!Uri.TryCreate(target, UriKind.Absolute, out Uri? candidateUri)
                || (candidateUri.Scheme != Uri.UriSchemeHttp && candidateUri.Scheme != Uri.UriSchemeHttps)
                || !A2AAgentOrigin.IsSameOrigin(agentOrigin, candidateUri))
            {
                continue;
            }

            if (isGrpc && (candidateUri.AbsolutePath != "/" || candidateUri.Query.Length != 0
                || candidateUri.Fragment.Length != 0 || candidateUri.UserInfo.Length != 0))
            {
                rejectedGrpcPrefix = true;
                continue;
            }

            if ((card.Capabilities?.Extensions ?? []).Any(extension =>
                string.Equals(extension.Uri, A2AInTaskAuthorizationClient.ExtensionUri, StringComparison.Ordinal)))
            {
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                    A2AInTaskAuthorizationClient.ExtensionHeader,
                    A2AInTaskAuthorizationClient.ExtensionUri);
            }
            return new A2AClientConnection(candidate.ProtocolBinding, candidateUri, httpClient);
        }

        throw new InvalidOperationException(
            rejectedGrpcPrefix
                ? "The advertised GRPC target must be an HTTP(S) authority at the root, without a path prefix, query, fragment, or user information."
                : $"The Agent Card advertises no supported interface on the configured agent origin '{agentOrigin.GetLeftPart(UriPartial.Authority)}'. "
                    + "Refusing to use an interface hosted elsewhere.");
    }

    private static bool IsSupportedBinding(string? protocolBinding)
        => string.Equals(protocolBinding, A2AProtocol.ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocolBinding, A2AProtocol.ProtocolBindingNames.HttpJson, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocolBinding, A2AProtocol.ProtocolBindingNames.Grpc, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesTransport(string? binding, A2ATransport transport) => transport switch
    {
        A2ATransport.Auto => true,
        A2ATransport.JsonRpc => string.Equals(binding, A2AProtocol.ProtocolBindingNames.JsonRpc, StringComparison.OrdinalIgnoreCase),
        A2ATransport.HttpJson => string.Equals(binding, A2AProtocol.ProtocolBindingNames.HttpJson, StringComparison.OrdinalIgnoreCase),
        A2ATransport.Grpc => string.Equals(binding, A2AProtocol.ProtocolBindingNames.Grpc, StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    internal static HttpClient CreateAgentHttpClient(HttpMessageHandler handler, Uri agentUrl, A2ATransport transport)
    {
        var client = new HttpClient(handler);
        if (agentUrl.Scheme == Uri.UriSchemeHttps)
        {
            client.DefaultRequestVersion = HttpVersion.Version20;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        }
        else if (transport == A2ATransport.Grpc)
        {
            client.DefaultRequestVersion = HttpVersion.Version20;
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        }
        return client;
    }

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

    internal static HttpClientHandler CreateNoRedirectHttpHandler()
        => new() { AllowAutoRedirect = false };

    internal static IReadOnlyList<IOAuthCredentialProvider> CreateCredentialProviders(
        A2ACliAuthenticationOptions options,
        IOAuthAuthorizationServerMetadataClient metadataClient,
        IDynamicClientRegistrationClient registrationClient,
        IOAuthClientRegistrationStore registrationStore,
        IOAuthProviderApproval approval)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(metadataClient);
        ArgumentNullException.ThrowIfNull(registrationClient);
        ArgumentNullException.ThrowIfNull(registrationStore);
        ArgumentNullException.ThrowIfNull(approval);

        return options.Providers.Values
            .Select(providerOptions => CreateCredentialProvider(
                providerOptions,
                metadataClient,
                registrationClient,
                registrationStore,
                approval))
            .ToArray();
    }

    private static IOAuthCredentialProvider CreateCredentialProvider(
        OAuthCredentialProviderOptions options,
        IOAuthAuthorizationServerMetadataClient metadataClient,
        IDynamicClientRegistrationClient registrationClient,
        IOAuthClientRegistrationStore registrationStore,
        IOAuthProviderApproval approval)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Type switch
        {
            OAuthCredentialProviderType.Entra => new EntraOAuthCredentialProvider(options),
            OAuthCredentialProviderType.GenericOAuth2 => new GenericOAuth2CredentialProvider(options),
            OAuthCredentialProviderType.GenericOAuth2Pkce => new GenericOAuth2PkceCredentialProvider(options),
            OAuthCredentialProviderType.OAuth21PkceDcr => new OAuth21DcrCredentialProvider(
                options,
                metadataClient,
                registrationClient,
                registrationStore,
                approval),
            _ => throw new InvalidOperationException(
                $"Unsupported OAuth credential provider type '{options.Type}'."),
        };
    }
}
