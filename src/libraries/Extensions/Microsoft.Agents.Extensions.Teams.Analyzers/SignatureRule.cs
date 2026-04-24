// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Microsoft.Agents.Extensions.Teams.Analyzers
{
    /// <summary>
    /// Describes the expected method signature for a single Teams route attribute.
    /// </summary>
    internal sealed class SignatureRule
    {
        /// <summary>Fully-qualified metadata name of the attribute type (e.g. "...QueryRouteAttribute").</summary>
        public string AttributeMetadataName { get; set; } = "";

        /// <summary>Short display name used in diagnostics (e.g. "QueryRoute").</summary>
        public string AttributeDisplayName { get; set; } = "";

        /// <summary>
        /// The fully-qualified metadata name of the generic type argument inside <c>Task&lt;T&gt;</c>.
        /// <c>null</c> means the method must return plain <c>Task</c> (non-generic).
        /// </summary>
        public string? ReturnTypeGenericArgument { get; set; }

        /// <summary>Human-readable expected return type used in diagnostic messages.</summary>
        public string ReturnTypeDisplayName { get; set; } = "";

        /// <summary>
        /// Expected parameter types, in order (0 = first parameter).
        /// A <c>null</c> entry means "accept any type" — used for generic <c>TData</c> parameters
        /// in <c>SubmitActionRoute</c>, <c>SelectItemRoute</c>, <c>CardButtonClickedRoute</c>, and
        /// meeting-participant routes.
        /// </summary>
        public string?[] ParameterTypes { get; set; } = [];
    }
}
