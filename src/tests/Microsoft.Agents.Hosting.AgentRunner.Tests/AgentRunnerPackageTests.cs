// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Microsoft.Agents.Hosting.AgentRunner.Tests;

public class AgentRunnerPackageTests
{
    [Fact]
    public async Task ToolPackage_InstallsAndRunsCompletePayload()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"runner-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var assembly = typeof(AgentRunnerPackageTests).Assembly;
            var project = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(attribute => attribute.Key == "AgentRunnerProjectPath").Value!;
            var configuration = assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
            var feed = Path.Combine(directory, "feed");
            var pack = await RunAsync("dotnet", Path.GetDirectoryName(project)!,
                "pack", project, "--no-build", "--no-restore", "-c", configuration, "-o", feed);
            Assert.True(pack.ExitCode == 0, pack.Output);
            using var package = ZipFile.OpenRead(Directory.GetFiles(feed, "*.nupkg").Single());
            using var manifest = package.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
            var version = XDocument.Load(manifest).Descendants().Single(element => element.Name.LocalName == "version").Value;

            var config = Path.Combine(directory, "NuGet.Config");
            new XDocument(new XElement("configuration",
                new XElement("packageSources",
                    new XElement("clear"),
                    new XElement("add", new XAttribute("key", "test-feed"), new XAttribute("value", feed)))))
                .Save(config);
            var tools = Path.Combine(directory, "tools");
            var install = await RunAsync("dotnet", directory,
                "tool", "install", "Microsoft.Agents.Hosting.AgentRunner", "--version", version,
                "--tool-path", tools, "--configfile", config);
            Assert.True(install.ExitCode == 0, install.Output);

            var runner = Path.GetDirectoryName(
                Directory.GetFiles(tools, "Microsoft.Agents.Hosting.AgentRunner.dll", SearchOption.AllDirectories).Single())!;
            foreach (var file in new[]
            {
                OperatingSystem.IsWindows() ? "Microsoft.Agents.Hosting.AgentRunner.exe" : "Microsoft.Agents.Hosting.AgentRunner",
                "Microsoft.Agents.Hosting.AgentRunner.dll",
                "Microsoft.Agents.Hosting.AgentRunner.deps.json",
                "Microsoft.Agents.Hosting.AgentRunner.runtimeconfig.json",
                "DotnetToolSettings.xml",
                "Microsoft.Agents.Hosting.AspNetCore.dll",
                "System.CommandLine.dll",
            })
            {
                Assert.True(File.Exists(Path.Combine(runner, file)), $"Missing runner payload: {file}");
            }

            var command = Path.Combine(tools, OperatingSystem.IsWindows() ? "agent-runner.exe" : "agent-runner");
            var run = await RunAsync(command, directory,
                "--port", "0", "--agent-assembly", "unused.dll");
            Assert.Equal(1, run.ExitCode);
            Assert.Contains("--port must be between 1 and 65535.", run.Output);

            foreach (var helpOption in new[] { "--help", "-h" })
            {
                var help = await RunAsync(command, directory, helpOption);
                Assert.Equal(0, help.ExitCode);
                Assert.Contains("--port", help.Output);
                Assert.Contains("--agent-assembly", help.Output);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(
        string command, string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        // Reusable MSBuild nodes would keep the redirected output pipes open.
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        if (arguments.Length > 0 && arguments[0] == "tool")
        {
            startInfo.Environment["NUGET_PACKAGES"] = Path.Combine(workingDirectory, "packages");
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try
        {
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var error = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            return (process.ExitCode, $"{await output}{await error}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }
}
