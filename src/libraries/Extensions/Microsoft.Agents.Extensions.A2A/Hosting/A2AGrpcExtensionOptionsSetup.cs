// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Grpc.AspNetCore.Server;
using A2A.Grpc.AspNetCore;
using Microsoft.Agents.Extensions.A2A.ProtocolExtensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Microsoft.Agents.Extensions.A2A.Hosting;

internal static class A2AGrpcExtensionOptionsSetup
{
    internal static void Configure(IServiceProvider provider, A2AEndpointDispatch dispatch)
    {
        // Upstream's service types are internal. Apply its public registrar's configurators before mapping creates handlers.
        var services = new ServiceCollection();
        services.AddA2AGrpc(dispatch.GrpcExtensions);
        foreach (var registration in services.Where(service =>
            service.ImplementationInstance != null
            && service.ServiceType.IsGenericType
            && service.ServiceType.GetGenericTypeDefinition() == typeof(IConfigureOptions<>)
            && service.ServiceType.GenericTypeArguments[0].IsGenericType
            && service.ServiceType.GenericTypeArguments[0].GetGenericTypeDefinition() == typeof(GrpcServiceOptions<>)))
        {
            var optionsType = registration.ServiceType.GenericTypeArguments[0];
            var options = (IOptions<object>)provider.GetRequiredService(typeof(IOptions<>).MakeGenericType(optionsType));
            try
            {
                registration.ServiceType.GetMethod(nameof(IConfigureOptions<object>.Configure))
                    .Invoke(registration.ImplementationInstance, [options.Value]);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }
    }
}
