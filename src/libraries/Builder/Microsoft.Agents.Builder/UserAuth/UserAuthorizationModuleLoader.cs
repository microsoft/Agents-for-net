// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication;
using Microsoft.Agents.Builder.Errors;
using Microsoft.Agents.Builder.UserAuth.TokenService;
using Microsoft.Agents.Core.Errors;
using Microsoft.Agents.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Agents.Builder.UserAuth
{
    internal class UserAuthorizationModuleLoader( AssemblyLoadContext loadContext, ILogger logger)
    {
        private readonly AssemblyLoadContext _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
        
        public ConstructorInfo GetProviderConstructor(string name, string assemblyName, string typeName)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (string.IsNullOrEmpty(assemblyName))
            {
                // A Assembly Lib name wasn't given in config.  Set to the default assembly lib
                assemblyName = typeof(AzureBotUserAuthorization).Assembly.GetName().Name;
                logger.LogInformation("No assembly name given in config for connection `{name}`.  Using default assembly lib: `{assemblyName}`", name, assemblyName);
            }

            if (string.IsNullOrEmpty(typeName))
            {
                // A Type name wasn't given in config.  Set to the default type name
                typeName = typeof(AzureBotUserAuthorization).FullName;
                logger.LogInformation("No type name given in config for connection `{name}`.  Using default type name: `{typeName}`", name, typeName);
            }

            // This throws for invalid assembly name.
            Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));

            Type type = assembly.GetType(typeName);
            if (!IsValidProviderType(type))
            {
                // Perhaps config left off the full type name?
                type = assembly.GetType($"{assemblyName}.{typeName}");
                if (!IsValidProviderType(type))
                {
                    throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.UserAuthorizationTypeNotFound, null, typeName, assemblyName, name);
                }
            }
            return GetConstructor(type) ?? throw ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateUserAuthorizationHandler, null, typeName, assemblyName); 
        }

        public IEnumerable<ConstructorInfo> GetProviderConstructors(string assemblyName)
        {
            ArgumentException.ThrowIfNullOrEmpty(assemblyName);

            Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));

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
                types: [typeof(string), typeof(IStorage), typeof(IConnections), typeof(IConfigurationSection)],
                modifiers: null);
        }
    }
}
