using System.Diagnostics;

namespace Microsoft.Agents.ApiCompat;

public sealed record ApiCompatExecution(int ExitCode, string StandardOutput, string StandardError);

public static class ApiCompatRunner
{
    public static async Task<ApiCompatExecution> RunAsync(
        string candidatePackage,
        string baselinePackage,
        bool strict,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePackage);
        ArgumentException.ThrowIfNullOrWhiteSpace(baselinePackage);

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = ResolveWorkingDirectory(),
        };

        startInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        startInfo.Environment["DOTNET_ROLL_FORWARD"] = "Major";

        foreach (var argument in GetArguments(candidatePackage, baselinePackage, strict))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start ApiCompat.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static IEnumerable<string> GetArguments(string candidatePackage, string baselinePackage, bool strict)
    {
        yield return "tool";
        yield return "run";
        yield return "apicompat";
        yield return "package";
        yield return candidatePackage;
        yield return "--baseline-package";
        yield return baselinePackage;
        yield return "--enable-rule-cannot-change-parameter-name";
        yield return "--noWarn";
        yield return DiagnosticClassifier.ApiCompatNoWarn;
        yield return "--verbosity";
        yield return "normal";

        if (strict)
        {
            yield return "--enable-strict-mode-for-baseline-validation";
        }
    }

    private static string ResolveWorkingDirectory()
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

        throw new InvalidOperationException("Unable to locate the local dotnet tool manifest.");
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
