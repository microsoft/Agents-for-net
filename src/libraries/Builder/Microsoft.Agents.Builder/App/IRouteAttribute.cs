// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Reflection;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Defines a contract for attributes that add routing information to an agent application.
    /// </summary>
    /// <remarks>Implementations of this interface are used to associate specific methods with routing logic
    /// in agent-based applications. This interface is typically used in frameworks that support attribute-based routing
    /// for extensibility.</remarks>
    public interface IRouteAttribute
    {
        /// <summary>
        /// Registers a route for the specified method within the given agent application.
        /// </summary>
        /// <param name="app">The agent application in which to register the route. Cannot be null.</param>
        /// <param name="method">The method information representing the route to add. Must not be null and should reference a valid method.</param>
        void AddRoute(AgentApplication app, MethodInfo method);
    }
}
