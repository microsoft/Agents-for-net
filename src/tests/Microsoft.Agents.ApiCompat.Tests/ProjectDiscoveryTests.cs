using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public sealed class ProjectDiscoveryTests
{
    [Fact]
    public void Discover_UsesPackageIdAndSkipsExplicitlyNonPackableProject()
    {
        using var fixture = RepositoryFixture.Create(
            ("src\\libraries\\A\\A.csproj", "<Project><PropertyGroup><PackageId>Contoso.A</PackageId></PropertyGroup></Project>"),
            ("src\\libraries\\B\\B.csproj", "<Project><PropertyGroup><IsPackable>false</IsPackable></PropertyGroup></Project>"));

        var result = ProjectDiscovery.Discover(fixture.Root);

        Assert.Collection(
            result,
            package =>
            {
                Assert.Equal(Path.Combine(fixture.Root, "src", "libraries", "A", "A.csproj"), package.ProjectPath);
                Assert.Equal("Contoso.A", package.PackageId);
            });
    }

    [Fact]
    public void Discover_FallsBackToProjectNameWhenPackageIdIsMissing()
    {
        using var fixture = RepositoryFixture.Create(
            ("src\\libraries\\Alpha\\Alpha.csproj", "<Project><PropertyGroup /></Project>"),
            ("src\\libraries\\Beta\\Beta.csproj", "<Project><PropertyGroup><PackageId>Contoso.Beta</PackageId></PropertyGroup></Project>"));

        var result = ProjectDiscovery.Discover(fixture.Root);

        Assert.Collection(
            result,
            package => Assert.Equal("Alpha", package.PackageId),
            package => Assert.Equal("Contoso.Beta", package.PackageId));
    }

    private sealed class RepositoryFixture : IDisposable
    {
        public string Root { get; }

        private RepositoryFixture(string root)
        {
            Root = root;
        }

        public static RepositoryFixture Create(params (string RelativePath, string Content)[] files)
        {
            var root = Path.Combine(AppContext.BaseDirectory, $"project-discovery-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            foreach (var (relativePath, content) in files)
            {
                var fullPath = Path.Combine(root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, content);
            }

            return new RepositoryFixture(root);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
