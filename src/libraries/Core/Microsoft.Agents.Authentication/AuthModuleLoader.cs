// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Agents.Authentication.Errors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Agents.Authentication
{
    internal class AuthModuleLoader(AssemblyLoadContext loadContext, ILogger logger)
    {
        private readonly AssemblyLoadContext _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));
        
        // Default auth lib for AgentSDK authentication.  
        private readonly string _defaultAuthenticationLib = "Microsoft.Agents.Authentication.Msal"; 
        private readonly string _defaultAuthenticationLibEntryType = "MsalAuth";

        public ConstructorInfo GetProviderConstructor(string name, string assemblyName, string typeName)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(name);

            if (string.IsNullOrEmpty(assemblyName))
            {
                // A Assembly Lib name wasn't given in config.  Set to the default assembly lib
                logger.LogInformation("No assembly name given in config for connection `{name}`.  Using default assembly lib: `{assemblyName}`", name, _defaultAuthenticationLib);
                assemblyName = _defaultAuthenticationLib; 
            }



            if (string.IsNullOrEmpty(typeName))
            {
                // A Type name wasn't given in config.  Set to the default type name
                logger.LogInformation("No type name given in config for connection `{name}`.  Using default type name: `{typeName}`", name, _defaultAuthenticationLibEntryType);
                typeName = _defaultAuthenticationLibEntryType;
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
                    throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.AuthProviderTypeNotFound, null, typeName, assemblyName, name);
                }
            }
            return GetConstructor(type) ?? throw Core.Errors.ExceptionHelper.GenerateException<InvalidOperationException>(ErrorHelper.FailedToCreateAuthModuleProvider, null, typeName, assemblyName); 
        }

        public IEnumerable<ConstructorInfo> GetProviderConstructors(string assemblyName)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(assemblyName);

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
                !typeof(IAccessTokenProvider).IsAssignableFrom(type) ||
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
                types: [typeof(IServiceProvider), typeof(IConfigurationSection)],
                modifiers: null);
        }
    }

}
