// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Validates that methods decorated with a route attribute (any attribute implementing
    /// <c>Microsoft.Agents.Builder.App.IRouteAttribute</c>) match the handler delegate signature the
    /// attribute expects.
    /// </summary>
    /// <remarks>
    /// The expected handler delegate is declared on the route attribute class with
    /// <c>[RouteHandlerType(typeof(TDelegate))]</c>, which is preserved in metadata. The analyzer reads that
    /// delegate's <c>Invoke</c> signature and compares it (return type and parameter types) to the decorated
    /// method. A mismatch surfaces at compile time instead of failing at runtime when the route is registered.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class RouteHandlerSignatureAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "MAA002";

        // Candidate signatures are encoded into a single diagnostic property for the code fix.
        // Records (one per acceptable delegate) are separated by RS; fields within a record by US.
        internal const string ExpectedCandidatesKey = "ExpectedCandidates";
        internal const char CandidateSeparator = '\u001E';   // record separator
        internal const char FieldSeparator = '\u001F';       // unit separator

        internal const string RouteAttributeInterfaceMetadataName = "Microsoft.Agents.Builder.App.IRouteAttribute";
        internal const string RouteHandlerTypeAttributeMetadataName = "Microsoft.Agents.Builder.App.RouteHandlerTypeAttribute";

        private static readonly DiagnosticDescriptor SignatureMismatchDescriptor = new(
            id: DiagnosticId,
            title: "Route handler method must match the expected handler delegate signature",
            messageFormat: "Method '{0}' decorated with '{1}' must match an expected route handler signature: {2}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Methods decorated with a route attribute are bound to the handler delegate the attribute declares. " +
                         "The method's return type and parameters must match that delegate's signature, otherwise route registration fails at runtime.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(SignatureMismatchDescriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(static compilationContext =>
            {
                var routeInterface = compilationContext.Compilation.GetTypeByMetadataName(RouteAttributeInterfaceMetadataName);
                var routeHandlerTypeAttribute = compilationContext.Compilation.GetTypeByMetadataName(RouteHandlerTypeAttributeMetadataName);

                // If the SDK isn't referenced, there is nothing to validate.
                if (routeInterface == null || routeHandlerTypeAttribute == null)
                {
                    return;
                }

                compilationContext.RegisterSymbolAction(
                    symbolContext => AnalyzeMethod(symbolContext, routeInterface, routeHandlerTypeAttribute),
                    SymbolKind.Method);
            });
        }

        private static void AnalyzeMethod(
            SymbolAnalysisContext context,
            INamedTypeSymbol routeInterface,
            INamedTypeSymbol routeHandlerTypeAttribute)
        {
            var method = (IMethodSymbol)context.Symbol;

            foreach (var attribute in method.GetAttributes())
            {
                var attributeClass = attribute.AttributeClass;
                if (attributeClass == null || !ImplementsInterface(attributeClass, routeInterface))
                {
                    continue;
                }

                var candidates = GetCandidateHandlerDelegates(attributeClass, routeHandlerTypeAttribute);
                if (candidates.Count == 0)
                {
                    // Nothing statically validatable: no declared handler type (e.g. the legacy RouteAttribute),
                    // or every declared handler is an unbound generic whose closed form is inferred at runtime.
                    continue;
                }

                if (candidates.Any(invoke => SignatureMatches(method, invoke)))
                {
                    continue;
                }

                var properties = ImmutableDictionary<string, string?>.Empty
                    .Add(ExpectedCandidatesKey, EncodeCandidates(candidates));

                var location = GetMethodSignatureLocation(method, attribute);

                context.ReportDiagnostic(Diagnostic.Create(
                    SignatureMismatchDescriptor,
                    location,
                    properties,
                    method.Name,
                    attributeClass.Name,
                    string.Join(" | ", candidates.Select(DescribeSignature))));
            }
        }

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
        {
            return type.AllInterfaces.Any(i => i.Equals(interfaceSymbol, SymbolEqualityComparer.Default));
        }

        /// <summary>
        /// Collects every distinct, statically-validatable handler delegate <c>Invoke</c> declared on the
        /// attribute class (and its base chain) via <c>RouteHandlerTypeAttribute</c>. Unbound generic
        /// delegates are skipped because their closed form is inferred from the decorated method at runtime.
        /// </summary>
        private static List<IMethodSymbol> GetCandidateHandlerDelegates(
            INamedTypeSymbol attributeClass,
            INamedTypeSymbol routeHandlerTypeAttribute)
        {
            var candidates = new List<IMethodSymbol>();
            var seen = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // RouteHandlerTypeAttribute is Inherited = true, so honor the base-class chain.
            var current = attributeClass;
            while (current != null)
            {
                foreach (var attr in current.GetAttributes())
                {
                    if (attr.AttributeClass == null ||
                        !attr.AttributeClass.Equals(routeHandlerTypeAttribute, SymbolEqualityComparer.Default) ||
                        attr.ConstructorArguments.Length != 1 ||
                        attr.ConstructorArguments[0].Value is not INamedTypeSymbol handlerType ||
                        handlerType.TypeKind != TypeKind.Delegate)
                    {
                        continue;
                    }

                    // Skip unbound/open generic delegates (e.g. FetchHandler<>) — the runtime infers the
                    // closed type argument from the method, so there is nothing to match statically.
                    if (IsOpenGeneric(handlerType) || handlerType.DelegateInvokeMethod is not { } invoke)
                    {
                        continue;
                    }

                    if (seen.Add(handlerType))
                    {
                        candidates.Add(invoke);
                    }
                }
                current = current.BaseType;
            }

            return candidates;
        }

        private static bool IsOpenGeneric(INamedTypeSymbol type)
        {
            return type.IsUnboundGenericType ||
                   type.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter);
        }

        private static bool SignatureMatches(IMethodSymbol method, IMethodSymbol invoke)
        {
            if (!method.ReturnType.Equals(invoke.ReturnType, SymbolEqualityComparer.Default))
            {
                return false;
            }

            if (method.Parameters.Length != invoke.Parameters.Length)
            {
                return false;
            }

            for (var i = 0; i < method.Parameters.Length; i++)
            {
                var methodParam = method.Parameters[i];
                var invokeParam = invoke.Parameters[i];

                if (methodParam.RefKind != invokeParam.RefKind ||
                    !methodParam.Type.Equals(invokeParam.Type, SymbolEqualityComparer.Default))
                {
                    return false;
                }
            }

            return true;
        }

        private static string EncodeCandidates(List<IMethodSymbol> candidates)
        {
            return string.Join(CandidateSeparator.ToString(), candidates.Select(EncodeCandidate));
        }

        private static string EncodeCandidate(IMethodSymbol invoke)
        {
            // delegateName US returnType US (param entries joined by '|', each "<fullyQualifiedType> <name>").
            var delegateName = invoke.ContainingType?.Name ?? "handler";
            var returnType = invoke.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var parameters = string.Join("|", invoke.Parameters.Select(p =>
                $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
            return string.Join(FieldSeparator.ToString(), new[] { delegateName, returnType, parameters });
        }

        private static string DescribeSignature(IMethodSymbol invoke)
        {
            var sb = new StringBuilder();
            sb.Append('(');
            for (var i = 0; i < invoke.Parameters.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(", ");
                }
                sb.Append(invoke.Parameters[i].Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            }
            sb.Append(") -> ");
            sb.Append(invoke.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            return sb.ToString();
        }

        private static Location GetMethodSignatureLocation(IMethodSymbol method, AttributeData attribute)
        {
            // Prefer the method's identifier location; fall back to the attribute application site.
            if (!method.Locations.IsDefaultOrEmpty && method.Locations[0].IsInSource)
            {
                return method.Locations[0];
            }

            var attrSyntax = attribute.ApplicationSyntaxReference;
            return attrSyntax != null
                ? Location.Create(attrSyntax.SyntaxTree, attrSyntax.Span)
                : Location.None;
        }
    }
}
