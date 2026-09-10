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
using System.IO;
using System.Linq;
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

            if (string.Equals(nameof(AzureBotUserAuthorization), typeName, StringComparison.OrdinalIgnoreCase))
            {
                typeName = typeof(AzureBotUserAuthorization).FullName;
            }
            else if (string.Equals(nameof(AgenticUserAuthorization), typeName, StringComparison.OrdinalIgnoreCase))
            {
                typeName = typeof(AgenticUserAuthorization).FullName;
            }
            else if (string.Equals(nameof(ConnectorUserAuthorization), typeName, StringComparison.OrdinalIgnoreCase))
            {
                typeName = typeof(ConnectorUserAuthorization).FullName;
            }

            if (string.IsNullOrEmpty(assemblyName) && !string.IsNullOrEmpty(typeName))
            {
                var loadedType = FindLoadedProviderType(typeName, name);
                if (loadedType != null)
                {
                    return GetConstructor(loadedType) ?? throw ExceptionHelper.GenerateException<InvalidOperationException>(
                        ErrorHelper.FailedToCreateUserAuthorizationHandler,
                        null,
                        loadedType.FullName,
                        loadedType.Assembly.GetName().Name);
                }
            }

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
#if !NETSTANDARD
                Assembly assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(assemblyName));
#else
            // This throws for invalid assembly name.
            Assembly assembly = _loadContext.Load(assemblyName);
#endif
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

        private Type FindLoadedProviderType(string typeName, string handlerName)
        {
#if !NETSTANDARD
            var assemblies = _loadContext.Assemblies;
#else
            var assemblies = _loadContext.GetAssemblies();
#endif
            var candidates = assemblies
                .Where(assembly => !assembly.IsDynamic)
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
                .SelectMany(GetLoadableTypes)
                .Where(IsValidProviderType)
                .ToArray();

            // A fully qualified name is unambiguous, so it wins over a bare type name that happens to
            // match in another assembly. This keeps resolution independent of assembly load order.
            var matches = candidates
                .Where(type => string.Equals(type.FullName, typeName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matches.Length == 0)
            {
                matches = candidates
                    .Where(type => string.Equals(type.Name, typeName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
            }

            if (matches.Length > 1)
            {
                var ambiguity = new AmbiguousMatchException(
                    $"Multiple IUserAuthorization types matched '{typeName}': {string.Join(", ", matches.Select(type => type.AssemblyQualifiedName))}");
                throw ExceptionHelper.GenerateException<InvalidOperationException>(
                    ErrorHelper.UserAuthorizationTypeNotFound,
                    ambiguity,
                    typeName,
                    "loaded assemblies",
                    handlerName);
            }

            return matches.SingleOrDefault();
        }

        /// <summary>
        /// Returns the types an assembly can supply for provider discovery.
        /// </summary>
        /// <remarks>
        /// Scanning every loaded assembly means an unrelated assembly whose dependencies cannot be
        /// resolved must not prevent user-authorization handlers from being created. Only the documented
        /// <see cref="Assembly.GetTypes"/> failures are handled, and each one is logged; any other
        /// exception propagates.
        /// </remarks>
        internal IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                logger.LogDebug(exception, "Some types in assembly `{assembly}` could not be loaded while resolving user authorization handlers.", assembly.FullName);
                return exception.Types.Where(type => type != null);
            }
            catch (Exception exception) when (
                exception is TypeLoadException
                || exception is FileNotFoundException
                || exception is FileLoadException
                || exception is BadImageFormatException)
            {
                logger.LogDebug(exception, "Assembly `{assembly}` could not be inspected while resolving user authorization handlers.", assembly.FullName);
                return [];
            }
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
