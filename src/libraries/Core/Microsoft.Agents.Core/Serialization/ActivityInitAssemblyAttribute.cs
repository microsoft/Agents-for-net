// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Assembly-level attribute that registers an <see cref="Microsoft.Agents.Core.Models.Activity"/> subclass
    /// decorated with <see cref="ActivityTypeAttribute"/> into <see cref="ProtocolJsonSerializer"/>.
    /// </summary>
    /// <remarks>
    /// Apply once per Activity subclass you want auto-registered:
    /// <code>
    /// [assembly: ActivityInitAssembly(typeof(MyCustomActivity))]
    ///
    /// [ActivityType("myCustomType")]
    /// public class MyCustomActivity : Activity { }
    /// </code>
    /// If the class has an <see cref="ActivityTypeAttribute.Resolver"/>, an instance of the resolver
    /// is created automatically and registered ahead of the default mapping for the same type string.
    /// </remarks>
    /// <param name="type">The Activity subclass to register. Must be decorated with <see cref="ActivityTypeAttribute"/>.</param>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class ActivityInitAssemblyAttribute(Type type) : Attribute
    {
        public readonly Type InitType = type;

        internal static void InitSerialization()
        {
            // Register for assemblies loaded after this point.
            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);

            // Process already-loaded assemblies.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                InitAssembly(assembly);
            }
        }

        private static void InitAssembly(Assembly assembly)
        {
            foreach (var type in GetRegisteredTypes(assembly))
            {
                try
                {
                    var attr = type.GetCustomAttribute<ActivityTypeAttribute>(false);
                    if (attr == null)
                    {
                        continue;
                    }

                    if (attr.Resolver != null)
                    {
                        var resolver = (IActivityTypeResolver)Activator.CreateInstance(attr.Resolver);
                        ProtocolJsonSerializer.AddActivityResolver(attr.ActivityType, type, resolver);
                    }
                    else
                    {
                        ProtocolJsonSerializer.AddActivityType(attr.ActivityType, type);
                    }
                }
                catch (Exception)
                {
                    // Ignore errors (e.g. duplicate keys). TODO: log this.
                }
            }
        }

        private static IEnumerable<Type> GetRegisteredTypes(Assembly assembly) =>
            assembly.GetCustomAttributes(typeof(ActivityInitAssemblyAttribute), false)
                    .OfType<ActivityInitAssemblyAttribute>()
                    .Select(a => a.InitType);
    }
}
