// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Core
{
    /// <summary>
    /// Identifies a type whose public static <c>Init</c> method participates in SDK initialization.
    /// </summary>
    /// <param name="type">The type containing the public static <c>Init</c> method.</param>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class AgentSdkInitAssemblyAttribute(Type type) : Attribute
    {
        /// <summary>
        /// Gets the type containing the initialization method.
        /// </summary>
        public Type InitType { get; } = type;
    }
}
