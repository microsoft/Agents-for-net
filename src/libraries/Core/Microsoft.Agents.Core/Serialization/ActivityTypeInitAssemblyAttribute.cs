// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microsoft.Agents.Core.Serialization
{
    /// <summary>
    /// Assembly-level attribute that points at a custom <see cref="Microsoft.Agents.Core.Models.Activity"/>
    /// subclass annotated with one or more <see cref="ActivityTypeAttribute"/> declarations.
    /// </summary>
    /// <remarks>
    /// One instance is emitted by the <c>ActivityTypeInitSourceGenerator</c> for each <c>[ActivityType]</c>
    /// class in an assembly. At <see cref="ProtocolJsonSerializer"/> initialization these attributes are
    /// read off the loaded assemblies and their declarations auto-registered, so extension authors do not
    /// need to call <see cref="ProtocolJsonSerializer.RegisterActivityTypes"/> manually, and the runtime
    /// never has to scan every type to find custom Activity subclasses.
    /// </remarks>
    /// <param name="type">The annotated custom <see cref="Microsoft.Agents.Core.Models.Activity"/> subclass.</param>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class ActivityTypeInitAssemblyAttribute(Type type) : Attribute
    {
        public readonly Type ActivityType = type;

        internal static void InitSerialization()
        {
            // Register handler for new assembly loads. This is needed because
            // C# doesn't load a package until accessed.
            AppDomain.CurrentDomain.AssemblyLoad += (s, o) => InitAssembly(o.LoadedAssembly);

            // Register from currently loaded assemblies.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                InitAssembly(assembly);
            }
        }

        internal static void InitAssembly(Assembly assembly)
        {
            foreach (var type in GetActivityTypes(assembly))
            {
                try
                {
                    // RegisterActivityTypes is idempotent, so re-processing an assembly is harmless.
                    ProtocolJsonSerializer.RegisterActivityTypes(new[] { type });
                }
                catch (Exception)
                {
                    // Ignore errors (e.g. a misconfigured [ActivityType] that sets no discriminators);
                    // a bad registration must not break serialization initialization for everything else.
                    // TODO: log this
                }
            }
        }

        private static IEnumerable<Type> GetActivityTypes(Assembly assembly) =>
            assembly
                .GetCustomAttributes(typeof(ActivityTypeInitAssemblyAttribute), false)?
                .OfType<ActivityTypeInitAssemblyAttribute>()
                .Select(x => x.ActivityType)
                ?? Array.Empty<Type>();
    }
}
