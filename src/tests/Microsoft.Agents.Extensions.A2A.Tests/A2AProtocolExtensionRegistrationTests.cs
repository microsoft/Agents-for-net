// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using Microsoft.Agents.Builder.App;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;
using Microsoft.Agents.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2AProtocolExtensionRegistrationTests
{
    [Fact]
    public async Task Contributors_ComposeDeclarationsAndOperationsBeforeFinalization()
    {
        var services = new ServiceCollection();
        services.AddA2AProtocolExtension<FirstAgent, DeclarationContributor>();
        services.AddA2AProtocolExtension<FirstAgent, OperationContributor>();
        await using var provider = services.BuildServiceProvider();

        var dispatch = provider.GetRequiredService<A2AProtocolExtensionRegistry>().GetDispatch(typeof(FirstAgent));
        var declaration = dispatch.Registry.Extensions["urn:example:profile"];

        Assert.Equal("A declaration-only profile", declaration.Description);
        Assert.True(declaration.Required);
        Assert.Equal(2, declaration.Params.Value.GetProperty("revision").GetInt32());
        Assert.Contains("urn:example:operation", dispatch.Registry.Extensions.Keys);
        Assert.Contains(InTaskAuthorizationExtension.Uri, dispatch.Registry.Extensions.Keys);
        Assert.Same(dispatch, provider.GetRequiredService<A2AProtocolExtensionRegistry>().GetDispatch(typeof(FirstAgent)));
    }

    [Fact]
    public async Task AgentScopedContributors_DoNotBecomeSupportForAnotherAgentOrHost()
    {
        var services = new ServiceCollection();
        services.AddA2AProtocolExtension<FirstAgent, DeclarationContributor>();
        services.AddA2AProtocolExtension<SecondAgent, OperationContributor>();
        await using var provider = services.BuildServiceProvider();
        var registries = provider.GetRequiredService<A2AProtocolExtensionRegistry>();

        var first = registries.GetDispatch(typeof(FirstAgent));
        var second = registries.GetDispatch(typeof(SecondAgent));
        Assert.Contains("urn:example:profile", first.Registry.Extensions.Keys);
        Assert.DoesNotContain("urn:example:operation", first.Registry.Extensions.Keys);
        Assert.Contains("urn:example:operation", second.Registry.Extensions.Keys);
        Assert.DoesNotContain("urn:example:profile", second.Registry.Extensions.Keys);

        var otherServices = new ServiceCollection();
        otherServices.AddA2AProtocolExtension<FirstAgent, OperationContributor>();
        await using var otherProvider = otherServices.BuildServiceProvider();
        var other = otherProvider.GetRequiredService<A2AProtocolExtensionRegistry>().GetDispatch(typeof(FirstAgent));
        Assert.Contains("urn:example:operation", other.Registry.Extensions.Keys);
        Assert.DoesNotContain("urn:example:profile", other.Registry.Extensions.Keys);
        Assert.NotSame(first.Registry, other.Registry);
    }

    [Fact]
    public async Task SingleAgentRegistration_RejectsAmbiguousMultiAgentMapping()
    {
        var services = new ServiceCollection();
        services.AddA2AProtocolExtension<OperationContributor>();
        await using var provider = services.BuildServiceProvider();
        var registries = provider.GetRequiredService<A2AProtocolExtensionRegistry>();

        Assert.Contains("urn:example:operation", registries.GetDispatch(typeof(FirstAgent)).Registry.Extensions.Keys);
        Assert.Throws<InvalidOperationException>(() =>
            registries.ValidateMappedAgents([typeof(FirstAgent), typeof(SecondAgent)]));
        Assert.Throws<InvalidOperationException>(() => registries.GetDispatch(typeof(SecondAgent)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedContributorRegistration_DoesNotRegisterTheExtensionTwice(bool includeSingleAgentRegistration)
    {
        var services = new ServiceCollection();
        services.AddA2AProtocolExtension<FirstAgent, OperationContributor>();
        services.AddA2AProtocolExtension<FirstAgent, OperationContributor>();
        if (includeSingleAgentRegistration)
        {
            services.AddA2AProtocolExtension<OperationContributor>();
        }
        await using var provider = services.BuildServiceProvider();

        var dispatch = provider.GetRequiredService<A2AProtocolExtensionRegistry>().GetDispatch(typeof(FirstAgent));

        Assert.Single(dispatch.Registry.Extensions.Values, extension => extension.Uri == "urn:example:operation");
    }

    [Theory]
    [InlineData("declaration")]
    [InlineData("operation")]
    [InlineData("json-rpc")]
    [InlineData("http")]
    public async Task Contributor_CannotReplaceSdkOwnedExtensionOrBindings(string collision)
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ConflictingContributor(collision));
        services.AddA2AProtocolExtension<FirstAgent, ConflictingContributor>();
        await using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<A2AProtocolExtensionRegistry>().GetDispatch(typeof(FirstAgent)));
    }

    public sealed class DeclarationContributor : IA2AProtocolExtensionContributor
    {
        public void Register(A2AProtocolExtensionBuilder builder)
        {
            builder.RegisterExtension(new global::A2A.AgentExtension
            {
                Uri = "urn:example:profile",
                Description = "A declaration-only profile",
                Required = true,
                Params = JsonSerializer.SerializeToElement(new { revision = 2 }),
            });
        }
    }

    public sealed class OperationContributor : IA2AProtocolExtensionContributor
    {
        public void Register(A2AProtocolExtensionBuilder builder)
        {
            var extension = builder.RegisterExtension(new global::A2A.AgentExtension { Uri = "urn:example:operation" });
            var operation = builder.Map(
                extension,
                new A2AOperationId("urn:example:operation#echo"),
                EchoAsync,
                StringTypeInfo,
                StringTypeInfo);
            builder.JsonRpc.Map("example/echo", operation);
            builder.Http.Map(HttpMethods.Post, "/example:echo", operation, ReadStringAsync);
        }
    }

    public sealed class ConflictingContributor(string collision) : IA2AProtocolExtensionContributor
    {
        public void Register(A2AProtocolExtensionBuilder builder)
        {
            var extension = builder.RegisterExtension(new global::A2A.AgentExtension
            {
                Uri = collision == "declaration" ? InTaskAuthorizationExtension.Uri : "urn:example:conflict",
            });
            var operation = builder.Map(
                extension,
                new A2AOperationId(collision == "operation"
                    ? InTaskAuthorizationExtension.AuthResponseOperationId
                    : "urn:example:conflict#echo"),
                EchoAsync,
                StringTypeInfo,
                StringTypeInfo);
            builder.JsonRpc.Map(collision == "json-rpc"
                ? InTaskAuthorizationExtension.AuthResponseOperation
                : "conflict/echo", operation);
            builder.Http.Map(HttpMethods.Post, collision == "http"
                ? "/tasks/{taskId}:auth-response"
                : "/conflict:echo", operation, ReadStringAsync);
        }
    }

    private static JsonTypeInfo<string> StringTypeInfo =>
        (JsonTypeInfo<string>)JsonSerializerOptions.Default.GetTypeInfo(typeof(string));

    private static ValueTask<string> EchoAsync(string request, CancellationToken cancellationToken) =>
        ValueTask.FromResult(request);

    private static async ValueTask<string> ReadStringAsync(HttpContext context, CancellationToken cancellationToken) =>
        await JsonSerializer.DeserializeAsync(context.Request.Body, StringTypeInfo, cancellationToken);

    private sealed class FirstAgent() : AgentApplication(new AgentApplicationOptions(new MemoryStorage()));
    private sealed class SecondAgent() : AgentApplication(new AgentApplicationOptions(new MemoryStorage()));
}
