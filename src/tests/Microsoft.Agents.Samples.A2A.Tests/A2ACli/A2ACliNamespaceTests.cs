// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Samples.A2ACli.Tests;

public class A2ACliNamespaceTests
{
    public static TheoryData<Type, string> ExpectedNamespaces => new()
    {
        { typeof(global::Microsoft.Agents.Samples.A2ACli.A2A.A2AAccessTokenProvider), "Microsoft.Agents.Samples.A2ACli.A2A" },
        { typeof(global::Microsoft.Agents.Samples.A2ACli.Configuration.A2ACliOptions), "Microsoft.Agents.Samples.A2ACli.Configuration" },
        { typeof(global::Microsoft.Agents.Samples.A2ACli.OAuth.Configuration.OAuthCredentialProviderOptions), "Microsoft.Agents.Samples.A2ACli.OAuth.Configuration" },
        { typeof(global::Microsoft.Agents.Samples.A2ACli.OAuth.OAuthCredentialBinding), "Microsoft.Agents.Samples.A2ACli.OAuth" },
        { typeof(global::Microsoft.Agents.Samples.A2ACli.OAuth.Providers.OAuthCredentialProviderResolver), "Microsoft.Agents.Samples.A2ACli.OAuth.Providers" },
        { typeof(global::Microsoft.Agents.Samples.A2ACli.OAuth.Tokens.OAuthTokenClient), "Microsoft.Agents.Samples.A2ACli.OAuth.Tokens" },
        { typeof(global::Microsoft.Agents.Samples.A2ACli.OAuth.Discovery.OAuthAuthorizationServerMetadataClient), "Microsoft.Agents.Samples.A2ACli.OAuth.Discovery" },
        { typeof(global::Microsoft.Agents.Samples.A2ACli.OAuth.Registration.DynamicClientRegistrationClient), "Microsoft.Agents.Samples.A2ACli.OAuth.Registration" },
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
                && string.Equals(type.Namespace, "Microsoft.Agents.Samples.A2ACli", StringComparison.Ordinal))
            .ToArray();

        Type programType = Assert.Single(rootNamespaceTypes);
        Assert.Same(typeof(Program), programType);
    }
}
