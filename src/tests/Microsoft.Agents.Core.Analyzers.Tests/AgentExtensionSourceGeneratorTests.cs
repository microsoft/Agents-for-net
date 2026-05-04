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

        // Two extensions whose type names use the "AgentExtension" suffix, so property names
        // derive to "Teams" and "Slack" — mirrors the real [TeamsExtension] + future [SlackExtension]
        // scenario.  Used across all multiple-attribute focused tests below.
        private const string TwoExtensionsSource = """
            namespace MyApp
            {
                public class TeamsAgentExtension : Microsoft.Agents.Builder.App.IAgentExtension
                {
                    public TeamsAgentExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                }
                public class SlackAgentExtension : Microsoft.Agents.Builder.App.IAgentExtension
                {
                    public SlackAgentExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                }

                public sealed class TeamsExtensionAttribute
                    : Microsoft.Agents.Builder.App.AgentExtensionAttribute<TeamsAgentExtension> { }
                public sealed class SlackExtensionAttribute
                    : Microsoft.Agents.Builder.App.AgentExtensionAttribute<SlackAgentExtension> { }

                [TeamsExtension]
                [SlackExtension]
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
            var driver = (CSharpGeneratorDriver)CSharpGeneratorDriver.Create(generator);
            driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updatedCompilation,
                out _);

            // Verify the generated source compiles without errors (warnings are allowed).
            // Generator-level diagnostics (e.g. MAA001) are checked in dedicated tests.
            var compileErrors = updatedCompilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();
            Assert.Empty(compileErrors);

            return (driver, updatedCompilation);
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
            Assert.Contains("public global::MyApp.MyExtension MyExtension { get; private set; }", text);
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

        // ---------------------------------------------------------------------------
        // Tests — multiple attributes (high-confidence coverage)
        // ---------------------------------------------------------------------------

        [Fact]
        public void MultipleExtensions_GeneratesPropertyForEachExtension()
        {
            var text = GetSingleGeneratedSource(StubSource, TwoExtensionsSource);

            Assert.Contains("public global::MyApp.TeamsAgentExtension TeamsExtension { get; private set; }", text);
            Assert.Contains("public global::MyApp.SlackAgentExtension SlackExtension { get; private set; }", text);
        }

        [Fact]
        public void MultipleExtensions_ConfigureExtensions_CallsBaseExactlyOnce()
        {
            var text = GetSingleGeneratedSource(StubSource, TwoExtensionsSource);

            Assert.Single(text.Split('\n'), l => l.Contains("base.ConfigureExtensions()"));
        }

        [Fact]
        public void MultipleExtensions_ConfigureExtensions_InitializesEachExtension()
        {
            var text = GetSingleGeneratedSource(StubSource, TwoExtensionsSource);

            Assert.Contains("TeamsExtension = new global::MyApp.TeamsAgentExtension(this);", text);
            Assert.Contains("SlackExtension = new global::MyApp.SlackAgentExtension(this);", text);
        }

        [Fact]
        public void MultipleExtensions_ConfigureExtensions_RegistersEachExtension()
        {
            var text = GetSingleGeneratedSource(StubSource, TwoExtensionsSource);

            Assert.Contains("RegisterExtension(TeamsExtension, _ => { });", text);
            Assert.Contains("RegisterExtension(SlackExtension, _ => { });", text);
        }

        [Fact]
        public void MultipleExtensions_ConfigureExtensions_InitializationPrecedesRegistration()
        {
            var text = GetSingleGeneratedSource(StubSource, TwoExtensionsSource);

            // For each extension the assignment must appear before its RegisterExtension call.
            var teamsInit = text.IndexOf("TeamsExtension = new global::MyApp.TeamsAgentExtension(this);", StringComparison.Ordinal);
            var teamsReg  = text.IndexOf("RegisterExtension(TeamsExtension,", StringComparison.Ordinal);
            var slackInit = text.IndexOf("SlackExtension = new global::MyApp.SlackAgentExtension(this);", StringComparison.Ordinal);
            var slackReg  = text.IndexOf("RegisterExtension(SlackExtension,", StringComparison.Ordinal);

            Assert.True(teamsInit >= 0 && teamsReg >= 0, "Teams init/register statements missing");
            Assert.True(slackInit >= 0 && slackReg >= 0, "Slack init/register statements missing");
            Assert.True(teamsInit < teamsReg, "Teams must be initialized before it is registered");
            Assert.True(slackInit < slackReg, "Slack must be initialized before it is registered");
        }

        [Fact]
        public void MultipleExtensions_ConfigureExtensions_BaseCalledBeforeExtensionInit()
        {
            var text = GetSingleGeneratedSource(StubSource, TwoExtensionsSource);

            var baseCall  = text.IndexOf("base.ConfigureExtensions()", StringComparison.Ordinal);
            var teamsInit = text.IndexOf("TeamsExtension = new global::MyApp.TeamsAgentExtension(this);", StringComparison.Ordinal);
            var slackInit = text.IndexOf("SlackExtension = new global::MyApp.SlackAgentExtension(this);", StringComparison.Ordinal);

            Assert.True(baseCall < teamsInit, "base.ConfigureExtensions() must precede Teams init");
            Assert.True(baseCall < slackInit, "base.ConfigureExtensions() must precede Slack init");
        }

        [Fact]
        public void MultipleExtensions_ProducesNoDiagnostics()
        {
            var (driver, _) = RunGenerator(StubSource, TwoExtensionsSource);
            Assert.Empty(driver.GetRunResult().Diagnostics);
        }

        [Fact]
        public void ThreeExtensions_GeneratesPropertyForEachAndRegistersAll()
        {
            var source = """
                namespace MyApp
                {
                    public class TeamsAgentExtension : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public TeamsAgentExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }
                    public class SlackAgentExtension : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public SlackAgentExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }
                    public class SharePointAgentExtension : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public SharePointAgentExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }

                    public sealed class TeamsExtensionAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<TeamsAgentExtension> { }
                    public sealed class SlackExtensionAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<SlackAgentExtension> { }
                    public sealed class SharePointExtensionAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<SharePointAgentExtension> { }

                    [TeamsExtension]
                    [SlackExtension]
                    [SharePointExtension]
                    public partial class MyAgent : Microsoft.Agents.Builder.App.AgentApplication { }
                }
                """;

            var text = GetSingleGeneratedSource(StubSource, source);

            // All three properties declared.
            Assert.Contains("public global::MyApp.TeamsAgentExtension TeamsExtension { get; private set; }", text);
            Assert.Contains("public global::MyApp.SlackAgentExtension SlackExtension { get; private set; }", text);
            Assert.Contains("public global::MyApp.SharePointAgentExtension SharePointExtension { get; private set; }", text);

            // All three initialized.
            Assert.Contains("TeamsExtension = new global::MyApp.TeamsAgentExtension(this);", text);
            Assert.Contains("SlackExtension = new global::MyApp.SlackAgentExtension(this);", text);
            Assert.Contains("SharePointExtension = new global::MyApp.SharePointAgentExtension(this);", text);

            // All three registered.
            Assert.Contains("RegisterExtension(TeamsExtension, _ => { });", text);
            Assert.Contains("RegisterExtension(SlackExtension, _ => { });", text);
            Assert.Contains("RegisterExtension(SharePointExtension, _ => { });", text);

            // Still exactly one ConfigureExtensions override and one base call.
            Assert.Single(text.Split('\n'), l => l.Contains("protected override void ConfigureExtensions()"));
            Assert.Single(text.Split('\n'), l => l.Contains("base.ConfigureExtensions()"));
        }

        [Fact]
        public void DuplicateExtensionAttribute_GeneratesOnlyOnePropertyAndRegistration()
        {
            // Two different attributes that both resolve to the same extension type must not
            // produce duplicate properties or duplicate initialization statements, which would
            // cause compilation errors.
            var source = """
                namespace MyApp
                {
                    public class FooAgentExtension : Microsoft.Agents.Builder.App.IAgentExtension
                    {
                        public FooAgentExtension(Microsoft.Agents.Builder.App.AgentApplication app) { }
                    }
                    public sealed class FooExtensionAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<FooAgentExtension> { }
                    public sealed class FooExtensionAliasAttribute
                        : Microsoft.Agents.Builder.App.AgentExtensionAttribute<FooAgentExtension> { }

                    [FooExtension]
                    [FooExtensionAlias]
                    public partial class MyAgent : Microsoft.Agents.Builder.App.AgentApplication { }
                }
                """;

            var text = GetSingleGeneratedSource(StubSource, source);

            // Exactly one property declaration.
            Assert.Single(
                text.Split('\n'),
                l => l.Contains("public global::MyApp.FooAgentExtension FooExtension { get; private set; }"));

            // Exactly one initialization and one registration.
            Assert.Single(
                text.Split('\n'),
                l => l.Contains("FooExtension = new global::MyApp.FooAgentExtension(this);"));
            Assert.Single(
                text.Split('\n'),
                l => l.Contains("RegisterExtension(FooExtension, _ => { });"));
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

            // "FooAgentExtension" → property name "FooExtension"
            Assert.Contains("public global::MyApp.FooAgentExtension FooExtension { get; private set; }", text);
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

            // "BarExtension" → property name "BarExtension"
            Assert.Contains("public global::MyApp.BarExtension BarExtension { get; private set; }", text);
        }

        [Fact]
        public void GeneratedFile_HasExpectedHintName()
        {
            var (driver, _) = RunGenerator(StubSource, SingleExtensionSource);
            var generated = Assert.Single(driver.GetRunResult().Results.Single().GeneratedSources);

            Assert.Equal("MyApp.MyAgent.AgentExtensions.g.cs", generated.HintName);
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
