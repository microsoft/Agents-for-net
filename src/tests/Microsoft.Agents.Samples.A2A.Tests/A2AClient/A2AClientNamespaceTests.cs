// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests;

public class A2AClientNamespaceTests
{
    public static TheoryData<Type, string> ExpectedNamespaces => new()
    {
        { typeof(global::Microsoft.Agents.Samples.A2AClient.A2A.A2AAccessTokenProvider), "Microsoft.Agents.Samples.A2AClient.A2A" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.Configuration.A2AClientOptions), "Microsoft.Agents.Samples.A2AClient.Configuration" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Configuration.OAuthCredentialProviderOptions), "Microsoft.Agents.Samples.A2AClient.OAuth.Configuration" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.OAuthCredentialBinding), "Microsoft.Agents.Samples.A2AClient.OAuth" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Providers.OAuthCredentialProviderResolver), "Microsoft.Agents.Samples.A2AClient.OAuth.Providers" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Tokens.OAuthTokenClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Tokens" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Discovery.OAuthAuthorizationServerMetadataClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Discovery" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Registration.DynamicClientRegistrationClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Registration" },
    };

    [Theory]
    [MemberData(nameof(ExpectedNamespaces))]
    public void Type_UsesExpectedNamespace(Type type, string expectedNamespace)
    {
        Assert.Equal(expectedNamespace, type.Namespace);
    }

    [Fact]
    public void RootNamespace_ContainsOnlyProgram()
    {
        Type[] rootNamespaceTypes = typeof(Program).Assembly.GetTypes()
            .Where(type =>
                !type.IsNested
                && string.Equals(type.Namespace, "Microsoft.Agents.Samples.A2AClient", StringComparison.Ordinal))
            .ToArray();

        Type programType = Assert.Single(rootNamespaceTypes);
        Assert.Same(typeof(Program), programType);
    }
}
