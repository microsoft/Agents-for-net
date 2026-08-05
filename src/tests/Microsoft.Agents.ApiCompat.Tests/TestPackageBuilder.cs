using System.Diagnostics;
using System.Text;

namespace Microsoft.Agents.ApiCompat.Tests;

public static class TestPackageBuilder
{
    public static async Task<string> BuildAsync(string packageId, string version, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        var root = Path.Combine(ResolveRepositoryRoot(), ".scratch", "apicompat-packages", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var projectPath = Path.Combine(root, "Fixture.csproj");
        await File.WriteAllTextAsync(
            projectPath,
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <PackageId>{{packageId}}</PackageId>
                <Version>{{version}}</Version>
                <AssemblyName>Fixture</AssemblyName>
                <BaseOutputPath>{{Path.Combine(root, "bin")}}{{Path.DirectorySeparatorChar}}</BaseOutputPath>
                <BaseIntermediateOutputPath>{{Path.Combine(root, "obj")}}{{Path.DirectorySeparatorChar}}</BaseIntermediateOutputPath>
              </PropertyGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(Path.Combine(root, "Api.cs"), source);

        await RunDotNetAsync(root, "pack", projectPath, "-c", "Release", "-o", root, "--nologo");
        return Directory.EnumerateFiles(root, "*.nupkg", SearchOption.AllDirectories)
            .Single(path => !path.EndsWith(".snupkg", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task RunDotNetAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
        };

        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode == 0)
        {
            return;
        }

        var message = new StringBuilder()
            .Append("dotnet")
            .Append(' ')
            .AppendJoin(' ', arguments)
            .Append(" failed with exit code ")
            .Append(process.ExitCode)
            .AppendLine(".")
            .AppendLine("STDOUT:")
            .AppendLine(standardOutput)
            .AppendLine("STDERR:")
            .Append(standardError)
            .ToString();

        throw new InvalidOperationException(message);
    }

    private static string ResolveRepositoryRoot()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, ".config", "dotnet-tools.json")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new InvalidOperationException("Unable to locate the repository root.");
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        yield return AppContext.BaseDirectory;

        var currentDirectory = Directory.GetCurrentDirectory();
        if (!string.Equals(currentDirectory, AppContext.BaseDirectory, StringComparison.Ordinal))
        {
            yield return currentDirectory;
        }
    }
}
