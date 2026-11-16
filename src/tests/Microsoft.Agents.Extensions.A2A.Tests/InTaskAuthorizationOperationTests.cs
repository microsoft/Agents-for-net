// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using A2A;
using A2A.AspNetCore;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions.InTaskAuthorization;

namespace Microsoft.Agents.Extensions.A2A.Tests;

public class InTaskAuthorizationOperationTests
{
    [Fact]
    public void AddOperation_DefinesStableTypedUnaryOperation()
    {
        var builder = new A2ACustomOperationRegistryBuilder();

        var registration = InTaskAuthorizationOperation.AddOperation(builder);
        var registry = builder.Build();

        Assert.Equal(
            InTaskAuthorizationExtension.AuthResponseOperationId,
            registration.Operation.Id.Value);
        Assert.Equal(InTaskAuthorizationExtension.Uri, registration.Extension.Uri);
        Assert.NotNull(registry);
    }

    [Fact]
    public void AddJsonRpcBinding_UsesAuthResponseMethod()
    {
        var operationBuilder = new A2ACustomOperationRegistryBuilder();
        var registration = InTaskAuthorizationOperation.AddOperation(operationBuilder);
        var registry = operationBuilder.Build();
        var bindingBuilder = new A2AJsonRpcCustomOperationBuilder();

        InTaskAuthorizationOperation.AddJsonRpcBinding(bindingBuilder, registration.Operation);

        Assert.NotNull(bindingBuilder.Build(registry));
    }

    [Fact]
    public void AddHttpBinding_UsesAuthResponseRoute()
    {
        var operationBuilder = new A2ACustomOperationRegistryBuilder();
        var registration = InTaskAuthorizationOperation.AddOperation(operationBuilder);
        var registry = operationBuilder.Build();
        var bindingBuilder = new A2AHttpCustomOperationBuilder();

        InTaskAuthorizationOperation.AddHttpBinding(bindingBuilder, registration.Operation);

        Assert.NotNull(bindingBuilder.Build(registry));
    }

    [Fact]
    public void SharedDispatch_UsesSimplifiedRegistryAndBindings()
    {
        Assert.NotNull(A2AServiceExtensions.Dispatch.Registry);
        Assert.NotNull(A2AServiceExtensions.Dispatch.JsonRpcBindings);
        Assert.NotNull(A2AServiceExtensions.Dispatch.HttpBindings);
    }
}
