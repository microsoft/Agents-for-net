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

        var operation = InTaskAuthorizationOperation.AddOperation(builder);
        var registry = builder.Build();

        Assert.Equal(
            InTaskAuthorizationExtension.ResumeAuthOperationId,
            operation.Id.Value);
        Assert.NotNull(registry);
    }

    [Fact]
    public void AddJsonRpcBinding_UsesResumeAuthMethod()
    {
        var operationBuilder = new A2ACustomOperationRegistryBuilder();
        var operation = InTaskAuthorizationOperation.AddOperation(operationBuilder);
        var registry = operationBuilder.Build();
        var bindingBuilder = new A2AJsonRpcCustomOperationBuilder();

        InTaskAuthorizationOperation.AddJsonRpcBinding(bindingBuilder, operation);

        Assert.NotNull(bindingBuilder.Build(registry));
    }

    [Fact]
    public void AddHttpBinding_UsesResumeAuthRoute()
    {
        var operationBuilder = new A2ACustomOperationRegistryBuilder();
        var operation = InTaskAuthorizationOperation.AddOperation(operationBuilder);
        var registry = operationBuilder.Build();
        var bindingBuilder = new A2AHttpCustomOperationBuilder();

        InTaskAuthorizationOperation.AddHttpBinding(bindingBuilder, operation);

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
