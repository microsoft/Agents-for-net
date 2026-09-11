// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Builder.UserAuth;
using Microsoft.Agents.Builder.UserAuth.Connector;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    public void GetProviderConstructor_WithBuiltInAliasAndNoAssembly_UsesBuiltInType()
    {
        var constructor = CreateLoader().GetProviderConstructor(
            "connector",
            null,
            nameof(ConnectorUserAuthorization));

        Assert.Equal(typeof(ConnectorUserAuthorization), constructor.DeclaringType);
    }

    /// <summary>
    /// Another loaded assembly declaring a handler with the same simple name must not change how the
    /// built-in alias resolves, and must not make resolution depend on assembly enumeration order.
    /// </summary>
    [Fact]
    public void GetProviderConstructor_WithCollidingSimpleNameInAnotherAssembly_StaysDeterministic()
    {
        var loader = CreateLoader();

        Assert.Equal(
            typeof(ConnectorUserAuthorization),
            loader.GetProviderConstructor("built-in", null, nameof(ConnectorUserAuthorization)).DeclaringType);

        Assert.Equal(
            typeof(Collisions.ConnectorUserAuthorization),
            loader.GetProviderConstructor("local", null, typeof(Collisions.ConnectorUserAuthorization).FullName).DeclaringType);
    }

    /// <summary>
    /// A configured extension type that is not present in any loaded assembly must produce the documented
    /// "type not found" failure rather than resolving to something else.
    /// </summary>
    [Fact]
    public void GetProviderConstructor_WithTypeNotInLoadedAssemblies_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateLoader().GetProviderConstructor("missing", null, "NotLoadedUserAuthorization"));

        Assert.Contains("NotLoadedUserAuthorization", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetLoadableTypes_WithReflectionTypeLoadException_ReturnsResolvedTypes()
    {
        var assembly = new ThrowingAssembly(new ReflectionTypeLoadException(
            [typeof(ConnectorUserAuthorization), null],
            [new TypeLoadException()]));

        var types = CreateLoader().GetLoadableTypes(assembly).ToArray();

        Assert.Equal([typeof(ConnectorUserAuthorization)], types);
    }

    [Theory]
    [MemberData(nameof(InspectionFailures))]
    public void GetLoadableTypes_WithInspectionFailure_SkipsAssembly(Exception failure)
    {
        var types = CreateLoader().GetLoadableTypes(new ThrowingAssembly(failure));

        Assert.Empty(types);
    }

    [Fact]
    public void GetLoadableTypes_WithUnexpectedFailure_Propagates()
    {
        var loader = CreateLoader();
        var assembly = new ThrowingAssembly(new InvalidTimeZoneException("unexpected"));

        Assert.Throws<InvalidTimeZoneException>(() => loader.GetLoadableTypes(assembly).ToArray());
    }

    public static TheoryData<Exception> InspectionFailures =>
    [
        new TypeLoadException(),
        new FileNotFoundException(),
        new FileLoadException(),
        new BadImageFormatException(),
    ];

    private static UserAuthorizationModuleLoader CreateLoader()
    {
#if NET8_0_OR_GREATER
        return new UserAuthorizationModuleLoader(AssemblyLoadContext.Default, NullLogger.Instance);
#else
        return new UserAuthorizationModuleLoader(AppDomain.CurrentDomain, NullLogger.Instance);
#endif
    }

    private sealed class ThrowingAssembly(Exception failure) : Assembly
    {
        public override bool IsDynamic => false;

        public override string FullName => "Throwing.Test.Assembly";

        public override Type[] GetTypes() => throw failure;
    }
}
