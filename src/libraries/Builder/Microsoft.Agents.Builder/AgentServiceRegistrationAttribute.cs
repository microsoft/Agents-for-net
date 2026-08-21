// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Agents.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Agents.Builder
{
    /// <summary>
    /// Declares an Agents SDK extension service registrar.
    /// </summary>
    /// <param name="registrarType">
    /// A type implementing <see cref="IAgentServiceRegistrar"/> with a parameterless constructor.
    /// </param>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class AgentServiceRegistrationAttribute(Type registrarType) : Attribute
    {
        private static readonly ConditionalWeakTable<IServiceCollection, HashSet<Type>> _appliedRegistrations = new();

        /// <summary>
        /// Gets the extension service registrar type.
        /// </summary>
        public Type RegistrarType { get; } = registrarType;

        internal static void ConfigureServices(IServiceCollection services)
        {
            AgentSdkInitializer.EnsureInitialized();

            foreach (var registration in GetRegistrations())
            {
                ValidateRegistrarType(registration);

                var appliedRegistrations = _appliedRegistrations.GetOrCreateValue(services);
                lock (appliedRegistrations)
                {
                    if (!appliedRegistrations.Add(registration))
                    {
                        continue;
                    }
                }

                try
                {
                    var registrar = (IAgentServiceRegistrar)Activator.CreateInstance(registration);
                    registrar.ConfigureServices(services);
                }
                catch
                {
                    lock (appliedRegistrations)
                    {
                        appliedRegistrations.Remove(registration);
                    }

                    throw;
                }
            }
        }

        internal static void ValidateRegistrarType(Type registration)
        {
            if (!typeof(IAgentServiceRegistrar).IsAssignableFrom(registration)
                || registration.IsAbstract
                || registration.IsInterface
                || !registration.IsPublic)
            {
                throw new InvalidOperationException(
                    $"{registration.FullName} must be a public, concrete {nameof(IAgentServiceRegistrar)}.");
            }

            if (registration.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException(
                    $"{registration.FullName} must have a public parameterless constructor.");
            }
        }

        private static IEnumerable<Type> GetRegistrations()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var attribute in assembly
                    .GetCustomAttributes(typeof(AgentServiceRegistrationAttribute), false)
                    .OfType<AgentServiceRegistrationAttribute>())
                {
                    if (attribute.RegistrarType != null)
                    {
                        yield return attribute.RegistrarType;
                    }
                }
            }
        }
    }
}
