// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Core.Analyzers.Tests
{
    public class RouteHandlerUnusedSuppressorTests
    {
        // Minimal SDK surface: the suppressor only needs IRouteAttribute and a route attribute that implements it.
        private const string StubSource = """
            using System;

            namespace Microsoft.Agents.Builder.App
            {
                public interface IRouteAttribute
                {
                }

                [AttributeUsage(AttributeTargets.Method)]
                public class MessageRouteAttribute : Attribute, IRouteAttribute
                {
                }
            }
            """;

        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);
        }

        private static async Task<ImmutableArray<Diagnostic>> GetSuppressedAwareDiagnosticsAsync(string source)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(StubSource), CSharpSyntaxTree.ParseText(source) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(compileErrors);

            // Run a stand-in IDE0051 analyzer together with the suppressor. reportSuppressedDiagnostics:true
            // keeps suppressed diagnostics in the result (with IsSuppressed=true) so suppression is observable;
            // otherwise programmatically-suppressed diagnostics are filtered out entirely.
            var analysisOptions = new CompilationWithAnalyzersOptions(
                options: new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                onAnalyzerException: (_, _, _) => { },
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true);

            var withAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new FakeUnusedMemberAnalyzer(),
                    new RouteHandlerUnusedSuppressor()),
                analysisOptions);

            return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        }

        [Fact]
        public async Task RouteHandlerMethod_Ide0051_IsSuppressed()
        {
            const string source = """
                using Microsoft.Agents.Builder.App;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MessageRoute]
                        private void WelcomeMessage() { }
                    }
                }
                """;

            var diags = await GetSuppressedAwareDiagnosticsAsync(source);
            var diag = Assert.Single(diags, d => d.Id == "IDE0051");
            Assert.True(diag.IsSuppressed);
        }

        [Fact]
        public async Task PlainPrivateMethod_Ide0051_IsNotSuppressed()
        {
            const string source = """
                namespace MyApp
                {
                    public class MyAgent
                    {
                        private void TrulyUnused() { }
                    }
                }
                """;

            var diags = await GetSuppressedAwareDiagnosticsAsync(source);
            var diag = Assert.Single(diags, d => d.Id == "IDE0051");
            Assert.False(diag.IsSuppressed);
        }

        [Fact]
        public async Task SdkNotReferenced_DoesNotSuppress()
        {
            // Compilation without the SDK stub: the suppressor must no-op rather than suppress everything.
            var compilation = CSharpCompilation.Create(
                "NoSdkAssembly",
                new[]
                {
                    CSharpSyntaxTree.ParseText("""
                        namespace MyApp
                        {
                            public class Plain
                            {
                                private void Unused() { }
                            }
                        }
                        """)
                },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var analysisOptions = new CompilationWithAnalyzersOptions(
                options: new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                onAnalyzerException: (_, _, _) => { },
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: true);

            var withAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new FakeUnusedMemberAnalyzer(),
                    new RouteHandlerUnusedSuppressor()),
                analysisOptions);

            var diags = await withAnalyzers.GetAnalyzerDiagnosticsAsync();
            var diag = Assert.Single(diags, d => d.Id == "IDE0051");
            Assert.False(diag.IsSuppressed);
        }

        /// <summary>
        /// Stand-in for the built-in IDE0051 analyzer: reports IDE0051 on every private, ordinary method so
        /// the suppressor has a diagnostic to act on without depending on Roslyn internals.
        /// </summary>
        [DiagnosticAnalyzer(LanguageNames.CSharp)]
        private sealed class FakeUnusedMemberAnalyzer : DiagnosticAnalyzer
        {
            private static readonly DiagnosticDescriptor Rule = new(
                id: "IDE0051",
                title: "Remove unused private members",
                messageFormat: "Private member '{0}' is unused",
                category: "CodeQuality",
                defaultSeverity: DiagnosticSeverity.Info,
                isEnabledByDefault: true);

            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
                ImmutableArray.Create(Rule);

            public override void Initialize(AnalysisContext context)
            {
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
                context.EnableConcurrentExecution();
                context.RegisterSymbolAction(ctx =>
                {
                    var method = (IMethodSymbol)ctx.Symbol;
                    if (method.DeclaredAccessibility == Accessibility.Private &&
                        method.MethodKind == MethodKind.Ordinary &&
                        !method.Locations.IsDefaultOrEmpty)
                    {
                        ctx.ReportDiagnostic(Diagnostic.Create(Rule, method.Locations[0], method.Name));
                    }
                }, SymbolKind.Method);
            }
        }
    }
}
