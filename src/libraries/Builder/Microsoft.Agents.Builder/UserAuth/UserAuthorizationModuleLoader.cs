// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.UserAuth.AgenticAuth;
using Microsoft.Agents.Builder.UserAuth.Connector;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Core;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
#if !NETSTANDARD
using System.Runtime.Loader;
#endif

namespace Microsoft.Agents.Builder.UserAuth
{
#if !NETSTANDARD
    internal class UserAuthorizationModuleLoader( AssemblyLoadContext loadContext, ILogger logger)
    {
        private readonly AssemblyLoadContext _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
#else
    internal class UserAuthorizationModuleLoader( AppDomain loadContext, ILogger logger)
    {
        private readonly AppDomain _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
#endif

        public ConstructorInfo GetProviderConstructor(string name, string assemblyName, string typeName)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(name, nameof(name));

            if (string.IsNullOrEmpty(typeName))
            {
                logger.LogInformation(
                    "No type name given in config for connection `{name}`. Using default type name: `{typeName}`",
                    name,
                    typeof(AzureBotUserAuthorization).FullName);
            }

            var builtInType = ResolveBuiltInProviderType(typeName);
            if (builtInType != null)
            {
                typeName = builtInType.FullName;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    assemblyName = builtInType.Assembly.GetName().Name;
                }
            }
            else if (string.IsNullOrEmpty(assemblyName))
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.UserAuthorizationAssemblyRequired,
                    null,
                    name,
                    typeName);
            }

            // This throws for invalid assembly name.
#if !NETSTANDARD
                Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));
#else
            // This throws for invalid assembly name.
            Assembly assembly = _loadContext.Load(assemblyName);
#endif
            Type type = ResolveProviderType(assembly, typeName);
            if (type == null)
            {
                throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationTypeNotFound, null, typeName, assemblyName, name);
            }

            return GetConstructor(type) ?? throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateUserAuthorizationHandler, null, typeName, assemblyName); 
        }

        private static Type ResolveProviderType(Assembly assembly, string typeName)
        {
            Type type = assembly.GetType(typeName);
            if (IsValidProviderType(type))
            {
                return type;
            }

            Type simpleNameMatch = null;
            foreach (Type candidate in assembly.GetTypes())
            {
                if (!IsValidProviderType(candidate)
                    || !string.Equals(candidate.Name, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (simpleNameMatch != null)
                {
                    return null;
                }

                simpleNameMatch = candidate;
            }

            return simpleNameMatch;
        }

        private static Type ResolveBuiltInProviderType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return typeof(AzureBotUserAuthorization);
            }

            foreach (var type in new[]
            {
                typeof(AzureBotUserAuthorization),
                typeof(AgenticUserAuthorization),
                typeof(ConnectorUserAuthorization),
            })
            {
                if (string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(type.FullName, typeName, StringComparison.OrdinalIgnoreCase))
                {
                    return type;
                }
            }

            return null;
        }

        public IEnumerable<ConstructorInfo> GetProviderConstructors(string assemblyName)
        {
            AssertionHelpers.ThrowIfNullOrEmpty(assemblyName, nameof(assemblyName));

#if !NETSTANDARD
            Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));
#else
            Assembly assembly = _loadContext.Load(assemblyName);
#endif

            foreach (Type loadedType in assembly.GetTypes())
            {
                if (!IsValidProviderType(loadedType))
                {
                    continue;
                }

                ConstructorInfo constructor = GetConstructor(loadedType);
                if (constructor == null)
                {
                    continue;
                }

                yield return constructor;
            }
        }

        private static bool IsValidProviderType(Type type)
        {
            if (type == null ||
                !typeof(IUserAuthorization).IsAssignableFrom(type) ||
                !type.IsPublic ||
                type.IsNested ||
                type.IsAbstract)
            {
                return false;
            }

            return true;
        }

        private static ConstructorInfo GetConstructor(Type type)
        {
            return type.GetConstructor(
                bindingAttr: BindingFlags.Instance | BindingFlags.Public,
                binder: null,
                types: [typeof(string), typeof(IStorage), typeof(IConnections), typeof(IConfigurationSection), typeof(ILogger)],
                modifiers: null);
        }
    }
}
