// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;

namespace Microsoft.Agents.Builder.App
{
    /// <summary>
    /// Declares the handler delegate type that a route attribute expects an attributed method to match.
    /// </summary>
    /// <remarks>
    /// Apply this attribute to an attribute class whose decorated methods are bound to a route handler
    /// delegate, whether through <see cref="IRouteAttribute"/> or extension-specific route registration.
    /// The declared type can be <see cref="RouteHandler"/>, <see cref="HandoffHandler"/>,
    /// <see cref="FeedbackLoopHandler"/>, or another route-handler delegate.
    /// <para>
    /// The handler type is recorded as an attribute argument so that it is preserved in compiled metadata.
    /// This allows a Roslyn analyzer running in a consuming project to read the expected handler delegate
    /// (which is not possible from a property getter body across an assembly boundary) and validate that the
    /// signature of each decorated method matches the delegate's <c>Invoke</c> signature.
    /// </para>
    /// <para>
    /// The attribute may be applied more than once to declare several acceptable handler delegates; a method
    /// is considered valid if it matches <em>any</em> of them. A declared <em>unbound</em> generic delegate
    /// (for example <c>typeof(FetchHandler&lt;&gt;)</c>) is not statically validated and is skipped by the
    /// analyzer, because the closed type is inferred from the decorated method at runtime.
    /// </para>
    /// </remarks>
    /// <param name="handlerType">The delegate <see cref="Type"/> that decorated methods must be assignable to.</param>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
    public sealed class RouteHandlerTypeAttribute(Type handlerType) : Attribute
    {
        /// <summary>
        /// Gets the delegate <see cref="Type"/> that decorated methods are expected to match.
        /// </summary>
        public Type HandlerType { get; } = handlerType;
    }
}
