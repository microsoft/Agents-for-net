// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using A2A;
using Microsoft.Agents.Samples.A2AClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microsoft.Agents.Samples.A2A.Tests;

public class A2AClientProgramTests
{
    [Theory]
    [InlineData("none", "none")]
    [InlineData("none", "delegated")]
    [InlineData("none", "app")]
    [InlineData("delegated", "none")]
    [InlineData("delegated", "delegated")]
    [InlineData("delegated", "app")]
    [InlineData("app", "none")]
    [InlineData("app", "delegated")]
    [InlineData("app", "app")]
    [InlineData("both", "none")]
    [InlineData("both", "delegated")]
    [InlineData("both", "app")]
    public async Task Main_DiscoversAnonymously_AndAuthNoneWorksWithoutOAuth(string cardFlows, string startupMode)
    {
        var requests = new ConcurrentQueue<(string Method, string Authorization)>();
        AgentCard card = CreateCard(cardFlows);
        await using WebApplication server = await StartAgentAsync(card, requests);

        var result = await RunClientAsync(server.Urls.Single(), startupMode, ":auth none\nhello\n\n:q\n");

        Assert.True(result.ExitCode == 0, $"Client exited {result.ExitCode}: {result.Error}");
        Assert.Contains("Authentication mode: none", result.Output, StringComparison.Ordinal);
        Assert.Contains("anonymous reply", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Request failed", result.Output, StringComparison.Ordinal);
        Assert.Collection(
            requests,
            request => Assert.Equal(("GET", ""), request),
            request => Assert.Equal(("POST", ""), request));
    }

    [Fact]
    public async Task Main_DiscoversAnonymously_WithTwoProtectedSkills_AndStillSendsNoBearerHeaderForAnonymousTraffic()
    {
        var requests = new ConcurrentQueue<(string Method, string Authorization)>();
        AgentCard card = CreateTwoProviderCard();
        await using WebApplication server = await StartAgentAsync(card, requests);

        var result = await RunClientAsync(server.Urls.Single(), "none", "hello\n\n:q\n");

        Assert.True(result.ExitCode == 0, $"Client exited {result.ExitCode}: {result.Error}");
        Assert.Collection(
            requests,
            request => Assert.Equal(("GET", ""), request),
            request => Assert.Equal(("POST", ""), request));
    }

    [Theory]
    [InlineData("none", "delegated", "Device Code")]
    [InlineData("none", "app", "Client Credentials")]
    [InlineData("delegated", "app", "Client Credentials")]
    [InlineData("app", "delegated", "Device Code")]
    public async Task Main_UnsupportedMode_FailsOnlyOnProtectedRequest_AndCanReturnToNone(
        string cardFlows,
        string mode,
        string expectedFlow)
    {
        var requests = new ConcurrentQueue<(string Method, string Authorization)>();
        AgentCard card = CreateCard(cardFlows);
        await using WebApplication server = await StartAgentAsync(card, requests);

        var result = await RunClientAsync(server.Urls.Single(), mode, "protected\n\n:auth none\nhello\n\n:q\n");

        Assert.True(result.ExitCode == 0, $"Client exited {result.ExitCode}: {result.Error}");
        Assert.Contains("Request failed: InvalidOperationException", result.Output, StringComparison.Ordinal);
        Assert.Contains($"{expectedFlow} authentication", result.Output, StringComparison.Ordinal);
        Assert.Contains("anonymous reply", result.Output, StringComparison.Ordinal);
        Assert.Collection(
            requests,
            request => Assert.Equal(("GET", ""), request),
            request => Assert.Equal(("POST", ""), request));
    }

    internal static AgentCard CreateCard(string flows)
    {
        var card = new AgentCard
        {
            Name = "OAuth startup test agent",
            Capabilities = new AgentCapabilities { Streaming = false },
            SecuritySchemes = [],
            SecurityRequirements = [],
        };
        if (flows is "delegated" or "both")
        {
            card.SecuritySchemes["delegated"] = new SecurityScheme
            {
                OAuth2SecurityScheme = new OAuth2SecurityScheme
                {
                    Flows = new OAuthFlows
                    {
                        DeviceCode = new()
                        {
                            TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                            DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                        },
                    },
                },
            };
            card.SecurityRequirements.Add(new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                },
            });
        }
        if (flows is "app" or "both")
        {
            card.SecuritySchemes["application"] = new SecurityScheme
            {
                OAuth2SecurityScheme = new OAuth2SecurityScheme
                {
                    Flows = new OAuthFlows
                    {
                        ClientCredentials = new()
                        {
                            TokenUrl = "https://login.microsoftonline.com/tenant-id/oauth2/v2.0/token",
                        },
                    },
                },
            };
            card.SecurityRequirements.Add(new SecurityRequirement
            {
                Schemes = new Dictionary<string, StringList>
                {
                    ["application"] = new() { List = ["api://agent/.default"] },
                },
            });
        }
        return card;
    }

    internal static AgentCard CreateTwoProviderCard()
    {
        return new AgentCard
        {
            Name = "Two-provider startup test agent",
            Capabilities = new AgentCapabilities { Streaming = false },
            SecuritySchemes = new Dictionary<string, SecurityScheme>
            {
                ["delegated"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                TokenUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token",
                                DeviceAuthorizationUrl = "https://login.microsoftonline.com/organizations/oauth2/v2.0/devicecode",
                            },
                        },
                    },
                },
                ["github"] = new()
                {
                    OAuth2SecurityScheme = new OAuth2SecurityScheme
                    {
                        Flows = new OAuthFlows
                        {
                            DeviceCode = new()
                            {
                                TokenUrl = "https://github.com/login/oauth/access_token",
                                DeviceAuthorizationUrl = "https://github.com/login/device/code",
                            },
                        },
                    },
                },
            },
            Skills =
            [
                new AgentSkill
                {
                    Id = "profile",
                    Name = "Microsoft Graph profile",
                    Examples = ["-me"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["delegated"] = new() { List = ["api://agent/access_as_user"] },
                            },
                        },
                    ],
                },
                new AgentSkill
                {
                    Id = "issues",
                    Name = "GitHub assigned issues",
                    Examples = ["-issues"],
                    SecurityRequirements =
                    [
                        new SecurityRequirement
                        {
                            Schemes = new Dictionary<string, StringList>
                            {
                                ["github"] = new() { List = ["repo"] },
                            },
                        },
                    ],
                },
            ],
        };
    }

    private static async Task<WebApplication> StartAgentAsync(
        AgentCard card,
        ConcurrentQueue<(string Method, string Authorization)> requests)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.Run(async context =>
        {
            requests.Enqueue((context.Request.Method, context.Request.Headers.Authorization.ToString()));
            if (HttpMethods.IsGet(context.Request.Method))
            {
                await context.Response.WriteAsJsonAsync(card, A2AJsonUtilities.DefaultOptions, context.RequestAborted);
            }
            else
            {
                await context.Response.WriteAsJsonAsync(
                    new SendMessageResponse
                    {
                        Message = new Message
                        {
                            MessageId = "reply",
                            Role = Role.Agent,
                            Parts = [Part.FromText("anonymous reply")],
                        },
                    },
                    A2AJsonUtilities.DefaultOptions,
                    context.RequestAborted);
            }
        });

        await app.StartAsync();
        card.SupportedInterfaces =
        [
            new AgentInterface
            {
                ProtocolBinding = ProtocolBindingNames.HttpJson,
                Url = $"{app.Urls.Single()}/a2a",
            },
        ];
        return app;
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunClientAsync(
        string agentUrl,
        string mode,
        string input)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Project references use the test host's shared frameworks instead of copying all client dependencies.
        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add("--runtimeconfig");
        startInfo.ArgumentList.Add(Path.ChangeExtension(typeof(A2AClientProgramTests).Assembly.Location, ".runtimeconfig.json"));
        startInfo.ArgumentList.Add("--depsfile");
        startInfo.ArgumentList.Add(Path.ChangeExtension(typeof(A2AClientProgramTests).Assembly.Location, ".deps.json"));
        startInfo.ArgumentList.Add(typeof(Program).Assembly.Location);
        startInfo.ArgumentList.Add("--agent");
        startInfo.ArgumentList.Add($"{agentUrl}/a2a");
        startInfo.ArgumentList.Add("--auth-mode");
        startInfo.ArgumentList.Add(mode);
        using var process = Process.Start(startInfo)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, await output, await error);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }
}
