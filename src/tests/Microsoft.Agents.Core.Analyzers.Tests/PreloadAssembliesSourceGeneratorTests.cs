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
        // ---------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Returns BCL platform references plus Microsoft.Agents.Core so tests can compile
        /// source that references <c>Microsoft.Agents.Core.Models.Entity</c>/<c>Activity</c>.
        /// </summary>
        private static IEnumerable<MetadataReference> GetReferences()
        {
            var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (trusted != null)
                foreach (var path in trusted.Split(Path.PathSeparator))
                    if (File.Exists(path))
                        yield return MetadataReference.CreateFromFile(path);

            yield return MetadataReference.CreateFromFile(
                typeof(Microsoft.Agents.Core.Models.Entity).Assembly.Location);
        }

        /// <summary>
        /// Compiles <paramref name="source"/> into a referenced assembly so the generator can
        /// discover derived types the way it would in a real referenced extension assembly.
        /// </summary>
        private static MetadataReference CreateReferencedAssembly(string assemblyName, string source)
        {
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { CSharpSyntaxTree.ParseText(source) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            return compilation.ToMetadataReference();
        }

        private static GeneratorDriverRunResult RunGenerator(params MetadataReference[] extraReferences)
        {
            var compilation = CSharpCompilation.Create(
                "TestAssembly",
                new[] { CSharpSyntaxTree.ParseText("namespace App { internal class Program { } }") },
                GetReferences().Concat(extraReferences),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new PreloadAssembliesSourceGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);
            return ((CSharpGeneratorDriver)driver.RunGenerators(compilation)).GetRunResult();
        }

        private static string GeneratedText(GeneratorDriverRunResult result) =>
            result.Results.Single().GeneratedSources.Single().SourceText.ToString();

        // ---------------------------------------------------------------------------
        // Tests
        // ---------------------------------------------------------------------------

        [Fact]
        public void ActivitySubclassInReferencedAssembly_IsPreloaded()
        {
            // The key new capability: custom Activity subclasses are discovered for preloading.
            var reference = CreateReferencedAssembly("Ext.Activities", """
                namespace Ext.Activities
                {
                    [Microsoft.Agents.Core.Serialization.ActivityType("x-custom")]
                    public class CustomActivity : Microsoft.Agents.Core.Models.Activity
                    {
                    }
                }
                """);

            var text = GeneratedText(RunGenerator(reference));

            Assert.Contains("typeof(global::Ext.Activities.CustomActivity)", text);
        }

        [Fact]
        public void EntitySubclassInReferencedAssembly_IsPreloaded()
        {
            var reference = CreateReferencedAssembly("Ext.Entities", """
                namespace Ext.Entities
                {
                    public class CustomEntity : Microsoft.Agents.Core.Models.Entity
                    {
                        public CustomEntity() : base("customEntity") { }
                    }
                }
                """);

            var text = GeneratedText(RunGenerator(reference));

            Assert.Contains("typeof(global::Ext.Entities.CustomEntity)", text);
        }

        [Fact]
        public void BothEntityAndActivitySubclasses_ArePreloaded()
        {
            var reference = CreateReferencedAssembly("Ext.Mixed", """
                namespace Ext.Mixed
                {
                    public class MixedActivity : Microsoft.Agents.Core.Models.Activity { }

                    public class MixedEntity : Microsoft.Agents.Core.Models.Entity
                    {
                        public MixedEntity() : base("mixedEntity") { }
                    }
                }
                """);

            var text = GeneratedText(RunGenerator(reference));

            Assert.Contains("typeof(global::Ext.Mixed.MixedActivity)", text);
            Assert.Contains("typeof(global::Ext.Mixed.MixedEntity)", text);
        }

        [Fact]
        public void IndirectActivitySubclass_IsPreloaded()
        {
            var reference = CreateReferencedAssembly("Ext.Indirect", """
                namespace Ext.Indirect
                {
                    public class BaseCustom : Microsoft.Agents.Core.Models.Activity { }
                    public class DerivedCustom : BaseCustom { }
                }
                """);

            var text = GeneratedText(RunGenerator(reference));

            Assert.Contains("typeof(global::Ext.Indirect.BaseCustom)", text);
            Assert.Contains("typeof(global::Ext.Indirect.DerivedCustom)", text);
        }

        [Fact]
        public void NonDerivedTypeInReferencedAssembly_IsNotPreloaded()
        {
            var reference = CreateReferencedAssembly("Ext.Plain", """
                namespace Ext.Plain
                {
                    public class NotDerived { }
                    public class AlsoNot : System.Exception { }
                }
                """);

            var result = RunGenerator(reference);

            // No derived types anywhere -> no generated output at all.
            Assert.Empty(result.Results.Single().GeneratedSources);
        }

        [Fact]
        public void CoreModelsSubclasses_AreExcluded()
        {
            // Microsoft.Agents.Core (which defines Activity/Entity and their built-in subclasses)
            // is always referenced, but its Models-namespace types must never be preloaded.
            var reference = CreateReferencedAssembly("Ext.Activities2", """
                namespace Ext.Activities2
                {
                    public class AnotherActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """);

            var text = GeneratedText(RunGenerator(reference));

            Assert.DoesNotContain("global::Microsoft.Agents.Core.Models", text);
            Assert.Contains("typeof(global::Ext.Activities2.AnotherActivity)", text);
        }

        [Fact]
        public void Generated_RegistersSerializationInitAssemblyAttribute()
        {
            var reference = CreateReferencedAssembly("Ext.Reg", """
                namespace Ext.Reg
                {
                    public class RegActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """);

            var text = GeneratedText(RunGenerator(reference));

            Assert.Contains(
                "[assembly: Microsoft.Agents.Core.Serialization.SerializationInitAssemblyAttribute(typeof(global::PreloadTypesRegistry))]",
                text);
            Assert.Contains("internal static class PreloadTypesRegistry", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            var reference = CreateReferencedAssembly("Ext.Hint", """
                namespace Ext.Hint
                {
                    public class HintActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """);

            var generated = RunGenerator(reference).Results.Single().GeneratedSources.Single();

            Assert.Equal("PreloadedAssemblies.g.cs", generated.HintName);
        }

        [Fact]
        public void Generator_ProducesNoDiagnostics()
        {
            var reference = CreateReferencedAssembly("Ext.Diag", """
                namespace Ext.Diag
                {
                    public class DiagActivity : Microsoft.Agents.Core.Models.Activity { }
                }
                """);

            var result = RunGenerator(reference);

            Assert.Empty(result.Diagnostics);
        }
    }
}
