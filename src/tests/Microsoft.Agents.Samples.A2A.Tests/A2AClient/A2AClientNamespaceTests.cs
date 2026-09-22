// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using Xunit;

namespace Microsoft.Agents.Samples.A2AClient.Tests;

public class A2AClientNamespaceTests
{
    public static TheoryData<Type, string> ExpectedNamespaces => new()
    {
        { typeof(global::Microsoft.Agents.Samples.A2AClient.Configuration.A2AClientOptions), "Microsoft.Agents.Samples.A2AClient.Configuration" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Configuration.OAuthCredentialProviderOptions), "Microsoft.Agents.Samples.A2AClient.OAuth.Configuration" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.OAuthCredentialBinding), "Microsoft.Agents.Samples.A2AClient.OAuth" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Discovery.OAuthAuthorizationServerMetadataClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Discovery" },
        { typeof(global::Microsoft.Agents.Samples.A2AClient.OAuth.Registration.DynamicClientRegistrationClient), "Microsoft.Agents.Samples.A2AClient.OAuth.Registration" },
    };

    [Theory]
    [MemberData(nameof(ExpectedNamespaces))]
    public void Type_UsesExpectedNamespace(Type type, string expectedNamespace)
    {
        Assert.Equal(expectedNamespace, type.Namespace);
    }
}
