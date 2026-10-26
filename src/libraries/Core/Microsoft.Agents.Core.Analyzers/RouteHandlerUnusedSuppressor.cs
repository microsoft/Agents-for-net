// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Suppresses the "unused private member" diagnostic (IDE0051) for methods decorated with a route
    /// attribute (an attribute implementing <c>Microsoft.Agents.Builder.App.IRouteAttribute</c> or
    /// declaring <c>Microsoft.Agents.Builder.App.RouteHandlerTypeAttribute</c>).
    /// </summary>
    /// <remarks>
    /// Route handlers are wired up declaratively through the attribute and invoked via route registration,
    /// so nothing references them syntactically. Without this suppressor the IDE greys them out and offers to
    /// remove them, even though they are required. The suppressor is scoped to methods carrying a route
    /// attribute, so genuinely unused private members are still reported.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class RouteHandlerUnusedSuppressor : DiagnosticSuppressor
    {
        // IDE0051: "Private member '...' is unused" (greyed out by the IDE).
        internal const string SuppressedDiagnosticId = "IDE0051";

        internal const string RouteAttributeInterfaceMetadataName = "Microsoft.Agents.Builder.App.IRouteAttribute";
        internal const string RouteHandlerTypeAttributeMetadataName = "Microsoft.Agents.Builder.App.RouteHandlerTypeAttribute";

        private static readonly SuppressionDescriptor Rule = new(
            id: "MAA1001",
            suppressedDiagnosticId: SuppressedDiagnosticId,
            justification: "Methods decorated with a route attribute are invoked via route registration, not direct references.");

        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
            ImmutableArray.Create(Rule);

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var routeInterface = context.Compilation.GetTypeByMetadataName(RouteAttributeInterfaceMetadataName);
            var routeHandlerTypeAttribute = context.Compilation.GetTypeByMetadataName(RouteHandlerTypeAttributeMetadataName);

            // If the SDK isn't referenced, there is nothing for this suppressor to act on.
            if (routeInterface == null || routeHandlerTypeAttribute == null)
            {
                return;
            }

            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                // ReportedDiagnostics is already filtered to our SupportedSuppressions, but guard explicitly
                // to avoid any semantic-model work for diagnostics we don't suppress.
                if (!string.Equals(diagnostic.Id, SuppressedDiagnosticId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                var tree = diagnostic.Location.SourceTree;
                if (tree == null)
                {
                    continue;
                }

                var root = tree.GetRoot(context.CancellationToken);
                var node = root.FindNode(diagnostic.Location.SourceSpan);
                var model = context.GetSemanticModel(tree);

                if (IsRouteHandlerMethod(node, model, routeInterface, routeHandlerTypeAttribute, context.CancellationToken))
                {
                    context.ReportSuppression(Suppression.Create(Rule, diagnostic));
                }
            }
        }

        private static bool IsRouteHandlerMethod(
            SyntaxNode node,
            SemanticModel model,
            INamedTypeSymbol routeInterface,
            INamedTypeSymbol routeHandlerTypeAttribute,
            CancellationToken cancellationToken)
        {
            // IDE0051 is reported on the member's identifier; walk up to the declaration and resolve the symbol.
            for (var current = node; current != null; current = current.Parent)
            {
                if (model.GetDeclaredSymbol(current, cancellationToken) is IMethodSymbol method)
                {
                    return HasRouteHandlerAttribute(method, routeInterface, routeHandlerTypeAttribute);
                }

                // Don't escape the containing type while searching for the member declaration.
                if (current is BaseTypeDeclarationSyntax)
                {
                    break;
                }
            }

            return false;
        }

        private static bool HasRouteHandlerAttribute(
            IMethodSymbol method,
            INamedTypeSymbol routeInterface,
            INamedTypeSymbol routeHandlerTypeAttribute)
        {
            return method.GetAttributes().Any(attr =>
                attr.AttributeClass != null &&
                (attr.AttributeClass.AllInterfaces.Any(i => i.Equals(routeInterface, SymbolEqualityComparer.Default)) ||
                 HasRouteHandlerType(attr.AttributeClass, routeHandlerTypeAttribute)));
        }

        private static bool HasRouteHandlerType(
            INamedTypeSymbol attributeClass,
            INamedTypeSymbol routeHandlerTypeAttribute)
        {
            for (var current = attributeClass; current != null; current = current.BaseType)
            {
                if (current.GetAttributes().Any(attr =>
                    attr.AttributeClass != null &&
                    attr.AttributeClass.Equals(routeHandlerTypeAttribute, SymbolEqualityComparer.Default)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
