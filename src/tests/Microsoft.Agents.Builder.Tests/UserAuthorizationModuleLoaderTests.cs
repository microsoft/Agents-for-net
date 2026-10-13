// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.Connector;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Extensions.Logging.Abstractions;
using System;
#if NET8_0_OR_GREATER
using System.Runtime.Loader;
#endif
using Xunit;

namespace Microsoft.Agents.Builder.Tests;

public class UserAuthorizationModuleLoaderTests
{
    [Fact]
    public void GetProviderConstructor_WithNoTypeOrAssembly_UsesAzureBotDefault()
    {
        var constructor = CreateLoader().GetProviderConstructor("default", null, null);

        Assert.Equal(typeof(AzureBotUserAuthorization), constructor.DeclaringType);
    }

    [Fact]
    public void GetProviderConstructor_WithExplicitAssembly_UsesThatAssembly()
    {
        var type = typeof(ConnectorUserAuthorization);
        var constructor = CreateLoader().GetProviderConstructor(
            "connector",
            type.Assembly.GetName().Name,
            type.FullName);

        Assert.Equal(type, constructor.DeclaringType);
    }

    [Fact]
    public void GetProviderConstructor_WithExternalSimpleTypeAndAssembly_UsesThatAssembly()
    {
        var type = typeof(ExternalUserAuthorization);
        var constructor = CreateLoader().GetProviderConstructor(
            "external",
            type.Assembly.GetName().Name,
            type.Name);

        Assert.Equal(type, constructor.DeclaringType);
    }

    [Fact]
    public void GetProviderConstructor_WithBuiltInAliasAndNoAssembly_UsesBuiltInType()
    {
        var constructor = CreateLoader().GetProviderConstructor(
            "connector",
            null,
            nameof(ConnectorUserAuthorization));

        Assert.Equal(typeof(ConnectorUserAuthorization), constructor.DeclaringType);
    }

    [Fact]
    public void GetProviderConstructor_WithExternalTypeAndNoAssembly_Throws()
    {
        var loader = CreateLoader();

        var exception = Assert.Throws<InvalidOperationException>(
            () => loader.GetProviderConstructor(
                "external",
                null,
                typeof(ExternalUserAuthorization).FullName));

        Assert.Contains("Assembly", exception.Message, StringComparison.Ordinal);
        Assert.Contains("external", exception.Message, StringComparison.Ordinal);
    }

    private static UserAuthorizationModuleLoader CreateLoader()
    {
#if NET8_0_OR_GREATER
        return new UserAuthorizationModuleLoader(AssemblyLoadContext.Default, NullLogger.Instance);
#else
        return new UserAuthorizationModuleLoader(AppDomain.CurrentDomain, NullLogger.Instance);
#endif
    }
}
