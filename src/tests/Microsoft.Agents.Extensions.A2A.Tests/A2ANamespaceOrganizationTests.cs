// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class A2ANamespaceOrganizationTests
{
    private const string RootNamespace = "Microsoft.Agents.Extensions.A2A";

    [Theory]
    [InlineData("A2ASkillRegistration", RootNamespace + ".Routing")]
    [InlineData("HandlerUtils", RootNamespace + ".Routing")]
    [InlineData("A2AServiceRegistrar", RootNamespace + ".Integration")]
    [InlineData("A2AAdapter", RootNamespace + ".Pipeline")]
    [InlineData("IA2AHttpAdapter", RootNamespace + ".Pipeline")]
    [InlineData("A2AJsonRpcProcessor", RootNamespace + ".Pipeline")]
    [InlineData("A2AMessageActivity", RootNamespace + ".Pipeline")]
    [InlineData("A2ATurnContext", RootNamespace + ".Pipeline")]
    [InlineData("A2AUserAuthorization", RootNamespace + ".Authorization")]
    [InlineData("A2AUserAuthorizationSettings", RootNamespace + ".Authorization")]
    [InlineData("A2AAgentCardOptions", RootNamespace + ".AgentCard")]
    [InlineData("IAgentCardHandler", RootNamespace + ".AgentCard")]
    [InlineData("SerializationInit", RootNamespace + ".Integration")]
    [InlineData("A2AAdapterOptions", RootNamespace)]
    [InlineData("A2AClient", RootNamespace)]
    [InlineData("BlobTaskStore", RootNamespace + ".Storage")]
    public void Type_UsesExpectedNamespace(string typeName, string expectedNamespace)
    {
        Type type = typeof(A2AAgentExtension).Assembly
            .GetTypes()
            .Single(candidate => candidate.Name == typeName);

        Assert.Equal(expectedNamespace, type.Namespace);
    }

    [Theory]
    [InlineData(typeof(A2AAgentExtension))]
    [InlineData(typeof(A2AExtensionAttribute))]
    [InlineData(typeof(IA2ATurnContext))]
    [InlineData(typeof(IA2AActivity))]
    [InlineData(typeof(A2ARouteHandler))]
    [InlineData(typeof(A2ASkillAttribute))]
    [InlineData(typeof(A2ASkillBuilder))]
    [InlineData(typeof(A2AMessageRouteAttribute))]
    [InlineData(typeof(A2AAgentTransportProtocol))]
    [InlineData(typeof(A2AAdapterOptions))]
    [InlineData(typeof(A2AClient))]
    [InlineData(typeof(A2AServiceExtensions))]
    [InlineData(typeof(A2AExtensions))]
    public void AuthoringType_RemainsInRootNamespace(Type type)
    {
        Assert.Equal(RootNamespace, type.Namespace);
    }

    [Fact]
    public void ExportedRootTypes_AreTheApprovedAuthoringSurface()
    {
        string[] expected =
        [
            "A2AAdapterOptions",
            "A2AAgentExtension",
            "A2AAgentTransportProtocol",
            "A2AClient",
            "A2AExtensionAttribute",
            "A2AExtensions",
            "A2AMessageRouteAttribute",
            "A2ARouteHandler",
            "A2AServiceExtensions",
            "A2ASkillAttribute",
            "A2ASkillBuilder",
            "IA2AActivity",
            "IA2ATurnContext",
        ];

        string[] actual = typeof(A2AAgentExtension).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == RootNamespace)
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
    }

    [Fact]
    public void ExportedTypes_AreTheApprovedPublicSurface()
    {
        string[] expected =
        [
            "Microsoft.Agents.Extensions.A2A.A2AAdapterOptions",
            "Microsoft.Agents.Extensions.A2A.A2AAgentExtension",
            "Microsoft.Agents.Extensions.A2A.A2AAgentTransportProtocol",
            "Microsoft.Agents.Extensions.A2A.A2AClient",
            "Microsoft.Agents.Extensions.A2A.A2AExtensionAttribute",
            "Microsoft.Agents.Extensions.A2A.A2AExtensions",
            "Microsoft.Agents.Extensions.A2A.A2AMessageRouteAttribute",
            "Microsoft.Agents.Extensions.A2A.A2ARouteHandler",
            "Microsoft.Agents.Extensions.A2A.A2AServiceExtensions",
            "Microsoft.Agents.Extensions.A2A.A2ASkillAttribute",
            "Microsoft.Agents.Extensions.A2A.A2ASkillBuilder",
            "Microsoft.Agents.Extensions.A2A.IA2AActivity",
            "Microsoft.Agents.Extensions.A2A.IA2ATurnContext",
            "Microsoft.Agents.Extensions.A2A.AgentCard.IAgentCardHandler",
            "Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorization",
            "Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorizationMode",
            "Microsoft.Agents.Extensions.A2A.Authorization.A2AUserAuthorizationSettings",
            "Microsoft.Agents.Extensions.A2A.Integration.A2AServiceRegistrar",
            "Microsoft.Agents.Extensions.A2A.Storage.BlobTaskStore",
        ];

        string[] actual = typeof(A2AAgentExtension).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
    }

    [Theory]
    [InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2AAdapter")]
    [InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.IA2AHttpAdapter")]
    [InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2AJsonRpcProcessor")]
    [InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2AMessageActivity")]
    [InlineData("Microsoft.Agents.Extensions.A2A.Pipeline.A2ATurnContext")]
    [InlineData("Microsoft.Agents.Extensions.A2A.AgentCard.A2AAgentCardOptions")]
    public void ImplementationType_IsNotExported(string fullName)
    {
        Assert.DoesNotContain(
            typeof(A2AAgentExtension).Assembly.GetExportedTypes(),
            type => type.FullName == fullName);
    }
}
