// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Agents.Core.Analyzers.Tests
{
    public class RouteHandlerSignatureAnalyzerTests
    {
        // Minimal SDK surface the analyzer resolves by metadata name, plus a couple of route attributes.
        private const string StubSource = """
            using System;

            namespace Microsoft.Agents.Builder
            {
                public interface ITurnContext { }
            }

            namespace Microsoft.Agents.Builder.State
            {
                public interface ITurnState { }
            }

            namespace Microsoft.Agents.Builder.App
            {
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                public delegate Task RouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken);
                public delegate Task HandoffHandler(ITurnContext turnContext, ITurnState turnState, string continuation, CancellationToken cancellationToken);
                public delegate Task<int> FetchHandler<T>(ITurnContext turnContext, ITurnState turnState, T data, CancellationToken cancellationToken);

                public interface IRouteAttribute
                {
                }

                [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
                public sealed class RouteHandlerTypeAttribute : Attribute
                {
                    public RouteHandlerTypeAttribute(Type handlerType) { HandlerType = handlerType; }
                    public Type HandlerType { get; }
                }

                [AttributeUsage(AttributeTargets.Method)]
                [RouteHandlerType(typeof(RouteHandler))]
                public class MessageRouteAttribute : Attribute, IRouteAttribute
                {
                }

                [AttributeUsage(AttributeTargets.Method)]
                [RouteHandlerType(typeof(HandoffHandler))]
                public class HandoffRouteAttribute : Attribute, IRouteAttribute
                {
                }

                // Open/unbound generic handler — closed type inferred at runtime, so the analyzer must skip it.
                [AttributeUsage(AttributeTargets.Method)]
                [RouteHandlerType(typeof(FetchHandler<>))]
                public class FetchRouteAttribute : Attribute, IRouteAttribute
                {
                }

                // Closed generic handler — fully concrete, so the analyzer validates it normally.
                [AttributeUsage(AttributeTargets.Method)]
                [RouteHandlerType(typeof(FetchHandler<string>))]
                public class FetchStringRouteAttribute : Attribute, IRouteAttribute
                {
                }

                // Declares two acceptable handler delegates — a method matching either is valid.
                [AttributeUsage(AttributeTargets.Method)]
                [RouteHandlerType(typeof(RouteHandler))]
                [RouteHandlerType(typeof(HandoffHandler))]
                public class MultiRouteAttribute : Attribute, IRouteAttribute
                {
                }

                // Implements IRouteAttribute but declares no RouteHandlerType — analyzer must ignore it.
                [AttributeUsage(AttributeTargets.Method)]
                public class LegacyRouteAttribute : Attribute, IRouteAttribute
                {
                }
            }
            """;

        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);
        }

        private static CSharpCompilation CreateCompilation(params string[] sources)
        {
            var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
            return CSharpCompilation.Create(
                "TestAssembly",
                trees,
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(params string[] sources)
        {
            var compilation = CreateCompilation(sources);

            // The stub itself must compile so we know diagnostics come from the analyzer, not parse/bind errors.
            var compileErrors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(compileErrors);

            var withAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new RouteHandlerSignatureAnalyzer()));
            return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
        }

        // ---------------------------------------------------------------------------
        // Diagnostic tests
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task ValidRouteHandlerSignature_ProducesNoDiagnostic()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MessageRoute]
                        public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
                            => Task.CompletedTask;
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.DoesNotContain(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task WrongReturnType_ReportsMAA002()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MessageRoute]
                        public void OnMessage(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken) { }
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.Single(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task WrongParameterType_ReportsMAA002()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MessageRoute]
                        public Task OnMessageAsync(ITurnContext turnContext, string wrong, CancellationToken cancellationToken)
                            => Task.CompletedTask;
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.Single(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task WrongParameterCount_ReportsMAA002()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MessageRoute]
                        public Task OnMessageAsync(ITurnContext turnContext, ITurnState turnState)
                            => Task.CompletedTask;
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.Single(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task HandoffHandlerSignature_IsValidatedAgainstItsOwnDelegate()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        // Missing the 'string continuation' parameter HandoffHandler requires.
                        [HandoffRoute]
                        public Task OnHandoffAsync(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
                            => Task.CompletedTask;
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.Single(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task LegacyAttributeWithoutHandlerType_IsIgnored()
        {
            const string source = """
                using Microsoft.Agents.Builder.App;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [LegacyRoute]
                        public void Whatever(int a, int b) { }
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.DoesNotContain(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task SdkNotReferenced_ProducesNoDiagnostic()
        {
            const string source = """
                namespace MyApp
                {
                    public class PlainClass
                    {
                        public void DoWork() { }
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(source);
            Assert.DoesNotContain(diags, d => d.Id == "MAA002");
        }

        // ---------------------------------------------------------------------------
        // Generic handler tests
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task OpenGenericHandler_IsSkipped_EvenForMismatchedSignature()
        {
            // FetchRoute declares typeof(FetchHandler<>); the closed type is inferred at runtime, so the
            // analyzer cannot (and must not) validate it — even a clearly wrong signature is left alone.
            const string source = """
                using Microsoft.Agents.Builder.App;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [FetchRoute]
                        public void OnFetch() { }
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.DoesNotContain(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task ClosedGenericHandler_MatchingSignature_ProducesNoDiagnostic()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [FetchStringRoute]
                        public Task<int> OnFetchAsync(ITurnContext turnContext, ITurnState turnState, string data, CancellationToken cancellationToken)
                            => Task.FromResult(0);
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.DoesNotContain(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task ClosedGenericHandler_WrongTypeArgument_ReportsMAA002()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        // 'int' instead of the closed 'string' the delegate expects.
                        [FetchStringRoute]
                        public Task<int> OnFetchAsync(ITurnContext turnContext, ITurnState turnState, int data, CancellationToken cancellationToken)
                            => Task.FromResult(0);
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.Single(diags, d => d.Id == "MAA002");
        }

        // ---------------------------------------------------------------------------
        // Multiple acceptable handler types (match-any)
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task MultipleHandlerTypes_MatchingFirst_ProducesNoDiagnostic()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MultiRoute]
                        public Task AsRouteHandler(ITurnContext turnContext, ITurnState turnState, CancellationToken cancellationToken)
                            => Task.CompletedTask;
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.DoesNotContain(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task MultipleHandlerTypes_MatchingSecond_ProducesNoDiagnostic()
        {
            const string source = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MultiRoute]
                        public Task AsHandoffHandler(ITurnContext turnContext, ITurnState turnState, string continuation, CancellationToken cancellationToken)
                            => Task.CompletedTask;
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            Assert.DoesNotContain(diags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task MultipleHandlerTypes_MatchingNeither_ReportsMAA002()
        {
            const string source = """
                using Microsoft.Agents.Builder.App;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MultiRoute]
                        public Task Neither(int wrong) => Task.CompletedTask;
                    }
                }
                """;

            var diags = await GetAnalyzerDiagnosticsAsync(StubSource, source);
            var diag = Assert.Single(diags, d => d.Id == "MAA002");
            // The message lists both acceptable signatures.
            Assert.Contains("|", diag.GetMessage());
        }

        // ---------------------------------------------------------------------------
        // Code fix test
        // ---------------------------------------------------------------------------

        [Fact]
        public async Task CodeFix_RewritesSignatureToMatchDelegate()
        {
            const string badMethod = """
                using Microsoft.Agents.Builder;
                using Microsoft.Agents.Builder.App;
                using Microsoft.Agents.Builder.State;
                using System.Threading;
                using System.Threading.Tasks;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MessageRoute]
                        public void OnMessage(int wrong) { }
                    }
                }
                """;

            using var workspace = new AdhocWorkspace();
            var project = workspace
                .AddProject("Test", LanguageNames.CSharp)
                .WithMetadataReferences(GetReferences())
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            project = project.AddDocument("Stub.cs", StubSource).Project;
            var document = project.AddDocument("Agent.cs", badMethod);

            var compilation = await document.Project.GetCompilationAsync();
            var withAnalyzers = compilation!.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new RouteHandlerSignatureAnalyzer()));
            var diagnostic = (await withAnalyzers.GetAnalyzerDiagnosticsAsync())
                .Single(d => d.Id == "MAA002");

            var actions = new List<CodeAction>();
            var fixContext = new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None);
            await new RouteHandlerSignatureCodeFixProvider().RegisterCodeFixesAsync(fixContext);

            var codeAction = Assert.Single(actions);
            var operations = await codeAction.GetOperationsAsync(CancellationToken.None);
            var applyChanges = operations.OfType<ApplyChangesOperation>().Single();
            var changedDocument = applyChanges.ChangedSolution.GetDocument(document.Id)!;
            var newText = (await changedDocument.GetTextAsync()).ToString();

            // Return type and parameters now match RouteHandler, written with simplified (not fully-qualified)
            // names because the file already imports the relevant namespaces.
            Assert.DoesNotContain("global::", newText);
            Assert.Contains("Task", newText);
            Assert.Contains("ITurnContext", newText);
            Assert.Contains("ITurnState", newText);
            Assert.Contains("CancellationToken", newText);

            // The fixed document no longer reports MAA002.
            var fixedCompilation = await changedDocument.Project.GetCompilationAsync();
            var fixedDiags = await fixedCompilation!
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new RouteHandlerSignatureAnalyzer()))
                .GetAnalyzerDiagnosticsAsync();
            Assert.DoesNotContain(fixedDiags, d => d.Id == "MAA002");
        }

        [Fact]
        public async Task CodeFix_MultipleHandlerTypes_OffersOneFixPerDelegate()
        {
            const string badMethod = """
                using Microsoft.Agents.Builder.App;

                namespace MyApp
                {
                    public class MyAgent
                    {
                        [MultiRoute]
                        public void Neither(int wrong) { }
                    }
                }
                """;

            using var workspace = new AdhocWorkspace();
            var project = workspace
                .AddProject("Test", LanguageNames.CSharp)
                .WithMetadataReferences(GetReferences())
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            project = project.AddDocument("Stub.cs", StubSource).Project;
            var document = project.AddDocument("Agent.cs", badMethod);

            var compilation = await document.Project.GetCompilationAsync();
            var diagnostic = (await compilation!
                    .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new RouteHandlerSignatureAnalyzer()))
                    .GetAnalyzerDiagnosticsAsync())
                .Single(d => d.Id == "MAA002");

            var actions = new List<CodeAction>();
            var fixContext = new CodeFixContext(
                document,
                diagnostic,
                (action, _) => actions.Add(action),
                CancellationToken.None);
            await new RouteHandlerSignatureCodeFixProvider().RegisterCodeFixesAsync(fixContext);

            // One fix per declared delegate (RouteHandler and HandoffHandler).
            Assert.Equal(2, actions.Count);
            Assert.Contains(actions, a => a.Title.Contains("RouteHandler"));
            Assert.Contains(actions, a => a.Title.Contains("HandoffHandler"));

            // Applying the HandoffHandler fix yields a method that no longer reports MAA002.
            var handoffFix = actions.Single(a => a.Title.Contains("HandoffHandler"));
            var operations = await handoffFix.GetOperationsAsync(CancellationToken.None);
            var changedDocument = operations.OfType<ApplyChangesOperation>().Single()
                .ChangedSolution.GetDocument(document.Id)!;
            var fixedDiags = await (await changedDocument.Project.GetCompilationAsync())!
                .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new RouteHandlerSignatureAnalyzer()))
                .GetAnalyzerDiagnosticsAsync();
            Assert.DoesNotContain(fixedDiags, d => d.Id == "MAA002");
        }
    }
}
