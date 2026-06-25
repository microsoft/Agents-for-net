// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Agents.Core.Analyzers
{
    /// <summary>
    /// Provides a code fix for <see cref="RouteHandlerSignatureAnalyzer"/> (MAA002) that rewrites a route
    /// handler method's parameter list and return type to match the expected handler delegate signature.
    /// When the attribute declares several acceptable handler delegates, one fix is offered per delegate.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(RouteHandlerSignatureCodeFixProvider))]
    [Shared]
    public class RouteHandlerSignatureCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(RouteHandlerSignatureAnalyzer.DiagnosticId);

        public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return;
            }

            foreach (var diagnostic in context.Diagnostics)
            {
                var node = root.FindNode(diagnostic.Location.SourceSpan);
                var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
                if (method == null)
                {
                    continue;
                }

                if (!diagnostic.Properties.TryGetValue(RouteHandlerSignatureAnalyzer.ExpectedCandidatesKey, out var encoded) ||
                    string.IsNullOrEmpty(encoded))
                {
                    continue;
                }

                var candidates = encoded!.Split(RouteHandlerSignatureAnalyzer.CandidateSeparator);

                // Disambiguate multiple offers that resolve to the same delegate name (rare).
                var titlesUsed = new HashSet<string>();

                foreach (var candidate in candidates)
                {
                    var fields = candidate.Split(RouteHandlerSignatureAnalyzer.FieldSeparator);
                    if (fields.Length != 3)
                    {
                        continue;
                    }

                    var delegateName = fields[0];
                    var returnType = fields[1];
                    var parameters = fields[2];

                    var title = candidates.Length == 1
                        ? "Change signature to match expected route handler"
                        : $"Change signature to match '{delegateName}'";
                    if (!titlesUsed.Add(title))
                    {
                        continue;
                    }

                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: title,
                            createChangedDocument: ct => ChangeSignatureAsync(context.Document, method, returnType, parameters, ct),
                            equivalenceKey: title),
                        diagnostic);
                }
            }
        }

        private static async Task<Document> ChangeSignatureAsync(
            Document document,
            MethodDeclarationSyntax method,
            string expectedReturnType,
            string encodedParameters,
            CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return document;
            }

            var newParameterList = BuildParameterList(encodedParameters, method.ParameterList);

            // Emit fully-qualified type names but tag them with Simplifier.Annotation so the reducer below
            // rewrites them to the shortest form valid with the file's using directives (e.g. "Task" rather
            // than "global::System.Threading.Tasks.Task").
            var newReturnType = SyntaxFactory.ParseTypeName(expectedReturnType)
                .WithAdditionalAnnotations(Simplifier.Annotation)
                .WithTriviaFrom(method.ReturnType);

            var newMethod = method
                .WithReturnType(newReturnType)
                .WithParameterList(newParameterList);

            var newRoot = root.ReplaceNode(method, newMethod);
            var newDocument = document.WithSyntaxRoot(newRoot);

            return await Simplifier.ReduceAsync(newDocument, Simplifier.Annotation, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        private static ParameterListSyntax BuildParameterList(string encodedParameters, ParameterListSyntax existing)
        {
            if (string.IsNullOrEmpty(encodedParameters))
            {
                return SyntaxFactory.ParameterList().WithTriviaFrom(existing);
            }

            // Each entry is "<fullyQualifiedType> <name>" joined by '|'. Reuse the existing parameter name
            // by position when one is available so callers' bodies keep referring to familiar identifiers.
            var entries = encodedParameters.Split('|');
            var parameters = new ParameterSyntax[entries.Length];

            for (var i = 0; i < entries.Length; i++)
            {
                var separator = entries[i].LastIndexOf(' ');
                var typeText = entries[i].Substring(0, separator);
                var defaultName = entries[i].Substring(separator + 1);

                var name = i < existing.Parameters.Count
                    ? existing.Parameters[i].Identifier.Text
                    : defaultName;

                parameters[i] = SyntaxFactory.Parameter(SyntaxFactory.Identifier(name))
                    .WithType(SyntaxFactory.ParseTypeName(typeText)
                        .WithAdditionalAnnotations(Simplifier.Annotation)
                        .WithTrailingTrivia(SyntaxFactory.Space));
            }

            return SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters))
                .WithTriviaFrom(existing);
        }
    }
}
