// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Microsoft.Agents.Core.Analyzers.Tests
{
    public class PreloadAssembliesSourceGeneratorTests
    {
        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);

            // Core assembly has SerializationInitAssemblyAttribute and EntityInitAssemblyAttribute
            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Core.Serialization.ProtocolJsonSerializer).Assembly.Location);
            // Builder assembly has StreamingResponseFactoryAssemblyAttribute
            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Builder.IStreamingResponseFactory).Assembly.Location);
        }

        private static CSharpGeneratorDriver RunGenerator(string source, IEnumerable<MetadataReference>? extraRefs = null)
        {
            var refs = GetReferences();
            if (extraRefs != null)
                refs = refs.Concat(extraRefs);

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new PreloadAssembliesSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            return (CSharpGeneratorDriver)driver.RunGenerators(compilation);
        }

        [Fact]
        public void NoReferencesWithKnownAttributes_ProducesNoOutput()
        {
            // Minimal source with no references to SDK assemblies that have the attributes.
            // We use a compilation that only references BCL (no Agents SDK refs).
            var source = """
                namespace MyApp
                {
                    public class Program { }
                }
                """;

            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var bclRefs = new List<MetadataReference>();
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        bclRefs.Add(MetadataReference.CreateFromFile(path));

            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                bclRefs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new PreloadAssembliesSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            var result = driver.RunGenerators(compilation).GetRunResult();

            Assert.Empty(result.Results.Single().GeneratedSources);
        }

        [Fact]
        public void ReferencedAssemblyWithKnownAttribute_GeneratesPreloader()
        {
            // The Microsoft.Agents.Core assembly has [assembly: SerializationInitAssemblyAttribute]
            // or [assembly: EntityInitAssemblyAttribute] entries (emitted by its generator).
            // When referenced, our preload generator should detect it and emit output.
            // We'll create a fake "extension" assembly with the attribute and reference it.

            // Step 1: Compile a fake extension that has the assembly attribute
            var extensionSource = """
                using System;

                [assembly: Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute(
                    typeof(FakeExtension.FakeFactory), "fakechannel")]

                namespace FakeExtension
                {
                    public class FakeFactory { }
                }
                """;

            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var bclRefs = new List<MetadataReference>();
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        bclRefs.Add(MetadataReference.CreateFromFile(path));
            bclRefs.Add(MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute).Assembly.Location));

            var extensionCompilation = CSharpCompilation.Create(
                "FakeExtension",
                new[] { CSharpSyntaxTree.ParseText(extensionSource) },
                bclRefs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Verify extension compiles
            var extensionDiags = extensionCompilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.Empty(extensionDiags);

            // Step 2: Reference the extension in a consuming project and run the preload generator
            var consumerSource = """
                namespace MyApp
                {
                    public class Program { }
                }
                """;

            var consumerRefs = bclRefs.ToList();
            consumerRefs.Add(extensionCompilation.ToMetadataReference());
            consumerRefs.Add(MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute).Assembly.Location));

            var consumerCompilation = CSharpCompilation.Create(
                "ConsumerApp",
                new[] { CSharpSyntaxTree.ParseText(consumerSource) },
                consumerRefs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new PreloadAssembliesSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            var result = driver.RunGenerators(consumerCompilation).GetRunResult();

            var generated = Assert.Single(result.Results.Single().GeneratedSources);
            var text = generated.SourceText.ToString();

            Assert.Contains("AgentAssemblyPreloader", text);
            Assert.Contains("ModuleInitializer", text);
            Assert.Contains("global::FakeExtension.FakeFactory", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            // Use same pattern as above with a fake extension
            var extensionSource = """
                using System;

                [assembly: Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute(
                    typeof(FakeExtension2.Factory), "test")]

                namespace FakeExtension2
                {
                    public class Factory { }
                }
                """;

            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            var bclRefs = new List<MetadataReference>();
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        bclRefs.Add(MetadataReference.CreateFromFile(path));
            bclRefs.Add(MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Builder.StreamingResponseFactoryAssemblyAttribute).Assembly.Location));

            var extensionCompilation = CSharpCompilation.Create(
                "FakeExtension2",
                new[] { CSharpSyntaxTree.ParseText(extensionSource) },
                bclRefs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var consumerRefs = bclRefs.ToList();
            consumerRefs.Add(extensionCompilation.ToMetadataReference());

            var consumerCompilation = CSharpCompilation.Create(
                "ConsumerApp2",
                new[] { CSharpSyntaxTree.ParseText("namespace X { public class Y { } }") },
                consumerRefs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new PreloadAssembliesSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            var result = driver.RunGenerators(consumerCompilation).GetRunResult();

            var generated = Assert.Single(result.Results.Single().GeneratedSources);
            Assert.Equal("AgentAssemblyPreloader.g.cs", generated.HintName);
        }

        [Fact]
        public void Generator_ProducesNoDiagnostics()
        {
            var source = """
                namespace MyApp { public class X { } }
                """;

            var result = RunGenerator(source).GetRunResult();
            Assert.Empty(result.Diagnostics);
        }

        [Fact]
        public void FrameworkAssemblies_AreSkipped()
        {
            // Even though System.* assemblies are referenced, they should not appear
            // in the generated preloader (they don't have known SDK attributes anyway,
            // but the skip logic ensures we don't even check them).
            var source = """
                namespace MyApp { public class X { } }
                """;

            var result = RunGenerator(source).GetRunResult();
            // If there are generated sources, they should not contain System.* types
            foreach (var gen in result.Results.SelectMany(r => r.GeneratedSources))
            {
                var text = gen.SourceText.ToString();
                Assert.DoesNotContain("global::System.", text);
            }
        }
    }
}
