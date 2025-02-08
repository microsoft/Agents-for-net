using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace Microsoft.Agents.Authentication
{
    internal class AuthModuleLoader(AssemblyLoadContext loadContext)
    {
        private readonly AssemblyLoadContext _loadContext = loadContext ?? throw new ArgumentNullException(nameof(loadContext));

        public ConstructorInfo GetProviderConstructor(string name, string assemblyName, string typeName)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(name);

            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentNullException(nameof(assemblyName), $"Assembly for '{name}' is missing or empty");
            }

            if (string.IsNullOrEmpty(typeName))
            {
                // A Type name wasn't given in config.  Just get the first matching valid type.
                // This is only really appropriate if an assembly only has a single IAccessTokenProvider.
                return GetProviderConstructors(assemblyName).First();
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
                    throw new InvalidOperationException($"Type '{typeName}' not found in Assembly '{assemblyName}' or is the wrong type for '{name}'");
                }
            }

            return GetConstructor(type) ?? throw new InvalidOperationException($"Type '{typeName},{assemblyName}' does not have the required constructor.");
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
