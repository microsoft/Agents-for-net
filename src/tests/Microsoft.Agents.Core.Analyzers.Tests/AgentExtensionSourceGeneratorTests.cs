// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Agents.Core.Analyzers.Tests
{
    public class AgentExtensionSourceGeneratorTests
    {
        // ---------------------------------------------------------------------------
        // Stub source that defines the minimal AgentApplication infrastructure
        // the generator looks for (must match exact qualified names used by the generator).
        // ---------------------------------------------------------------------------

        private const string StubSource = """
            namespace Microsoft.Agents.Builder.App
            {
                public interface IAgentExtension { }
                public abstract class AgentExtensionAttribute<T> : System.Attribute { }
                public class AgentApplication
                {
                    protected virtual void ConfigureExtensions() { }
                    public System.Collections.Generic.List<IAgentExtension> RegisteredExtensions { get; } = new();
                    public void RegisterExtension<T>(T ext, System.Action<T> reg) where T : IAgentExtension { }
                }
            }
            """;

        // A minimal extension type and its attribute, used across several tests.
        private const string SingleExtensionSource = """
            namespace MyApp
            {
                public class MyExtension : Microsoft.Agents.Builder.App.IAgentExtension
                {
                    public MyExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                }

                public sealed class MyExtensionAttribute
                    : Microsoft.Agents.Builder.App.AgentExtensionAttribute<MyExtension> { }

                [MyExtension]
                public partial class MyAgent : Microsoft.Agents.Builder.App.AgentApplication { }
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

        private static (CSharpGeneratorDriver driver, Compilation compilation) RunGenerator(params string[] sources)
        {
            var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                trees,
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new AgentExtensionSourceGenerator();
            var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver.Create(generator)
                .RunGenerators(compilation);
            return (driver, compilation);
        }

        private static string GetSingleGeneratedSource(params string[] sources)
        {
            var (driver, _) = RunGenerator(sources);
            var generated = Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources);
            return generated.SourceText.ToString();
        }

        // ---------------------------------------------------------------------------
        // Tests — output shape
        // ---------------------------------------------------------------------------

        [Fact]
        public void NoAgentExtensionAttributes_ProducesNoOutput()
        {
            var source = """
                namespace MyApp
                {
                    public class PlainClass { }
                }
                """;

            var (driver, _) = RunGenerator(StubSource, source);
            Assert.Empty(driver.GetRunResult().Results.Single().GeneratedSources);
        }

        [Fact]
        public void SingleExtension_GeneratesAutoProperty()
        {
            var text = GetSingleGeneratedSource(StubSource, SingleExtensionSource);

            // Auto-property with private set — no separate backing field, no lazy null check.
            Assert.Contains("public global::MyApp.MyExtension My { get; private set; }", text);
            Assert.DoesNotContain("if (_my", text);
        }

        [Fact]
        public void SingleExtension_GeneratesConfigureExtensionsOverride()
        {
            var text = GetSingleGeneratedSource(StubSource, SingleExtensionSource);

            Assert.Contains("protected override void ConfigureExtensions()", text);
        }

        [Fact]
        public void SingleExtension_ConfigureExtensions_CallsBase()
        {
            var text = GetSingleGeneratedSource(StubSource, SingleExtensionSource);

            Assert.Contains("base.ConfigureExtensions()", text);
        }

        [Fact]
        public void SingleExtension_ConfigureExtensions_InitializesFieldAndRegisters()
        {
            var text = GetSingleGeneratedSource(StubSource, SingleExtensionSource);

            Assert.Contains("new global::MyApp.MyExtension(this)", text);
            Assert.Contains("RegisterExtension(", text);
        }

        [Fact]
        public void MultipleExtensions_GeneratesSingleConfigureExtensionsOverride()
        {
            var source = """
                namespace MyApp
                {
                    public class ExtA : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public ExtA(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }
                    public class ExtB : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public ExtB(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }

                    public sealed class ExtAAttribute : Microsoft.Agents.Builder.App.AgentExtensionAttribute<ExtA> { }
                    public sealed class ExtBAttribute : Microsoft.Agents.Builder.App.AgentExtensionAttribute<ExtB> { }

                    [ExtA]
                    [ExtB]
                    public partial class MyAgent : Microsoft.Agents.Builder.App.AgentApplication { }
                }
                """;

            var text = GetSingleGeneratedSource(StubSource, source);

            // Exactly one override method.
            Assert.Single(
                text.Split('\n'),
                l => l.Contains("protected override void ConfigureExtensions()"));

            // Both extensions initialized within it.
            Assert.Contains("new global::MyApp.ExtA(this)", text);
            Assert.Contains("new global::MyApp.ExtB(this)", text);
        }

        [Fact]
        public void NonPartialClass_ProducesDiagnosticAndNoSource()
        {
            var source = """
                namespace MyApp
                {
                    public class MyExt : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public MyExt(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }
                    public sealed class MyExtAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<MyExt> { }

                    [MyExt]
                    public class NonPartialAgent : Microsoft.Agents.Builder.App.AgentApplication { }
                }
                """;

            var (driver, _) = RunGenerator(StubSource, source);
            var result = driver.GetRunResult().Results.Single();

            Assert.Empty(result.GeneratedSources);
            Assert.Single(result.Diagnostics, d => d.Id == "MAA001");
        }

        [Fact]
        public void PropertyNameDerived_ByStrippingAgentExtensionSuffix()
        {
            var source = """
                namespace MyApp
                {
                    public class FooAgentExtension : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public FooAgentExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }
                    public sealed class FooExtensionAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<FooAgentExtension> { }

                    [FooExtension]
                    public partial class MyAgent : Microsoft.Agents.Builder.App.AgentApplication { }
                }
                """;

            var text = GetSingleGeneratedSource(StubSource, source);

            // "FooAgentExtension" → property name "Foo"
            Assert.Contains("public global::MyApp.FooAgentExtension Foo { get; private set; }", text);
        }

        [Fact]
        public void PropertyNameDerived_ByStrippingExtensionSuffix()
        {
            var source = """
                namespace MyApp
                {
                    public class BarExtension : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public BarExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }
                    public sealed class BarExtensionAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<BarExtension> { }

                    [BarExtension]
                    public partial class MyAgent : Microsoft.Agents.Builder.App.AgentApplication { }
                }
                """;

            var text = GetSingleGeneratedSource(StubSource, source);

            // "BarExtension" → property name "Bar"
            Assert.Contains("public global::MyApp.BarExtension Bar { get; private set; }", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            var (driver, _) = RunGenerator(StubSource, SingleExtensionSource);
            var generated = Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources);

            Assert.Equal("MyAgent.AgentExtensions.g.cs", generated.HintName);
        }

        [Fact]
        public void Generator_ProducesNoDiagnostics_ForValidInput()
        {
            var (driver, _) = RunGenerator(StubSource, SingleExtensionSource);
            Assert.Empty(driver.GetRunResult().Diagnostics);
        }

        [Fact]
        public void IncrementalCaching_DoesNotRerunWhenUnrelatedFileChanges()
        {
            var trees = new[]
            {
                CSharpSyntaxTree.ParseText(StubSource),
                CSharpSyntaxTree.ParseText(SingleExtensionSource)
            };
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                trees,
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new AgentExtensionSourceGenerator();
            var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver.Create(generator)
                .RunGenerators(compilation);
            var first = driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();

            driver = (CSharpGeneratorDriver)driver.RunGenerators(compilation);
            var second = driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();

            Assert.Equal(first, second);
        }
    }
}
