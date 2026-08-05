using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public class PullRequestEventTests
{
    [Fact]
    public void Load_ParsesValidPullRequestEvent()
    {
        using var scope = new EnvironmentVariableScope("GITHUB_RUN_ID", "12345");
        var path = WriteEvent("""
            {
              "pull_request": {
                "number": 7,
                "base": { "ref": "main" },
                "body": "## Details\nApproved",
                "labels": [
                  { "name": "breaking-change-approved" },
                  { "name": "api" }
                ]
              }
            }
            """);

        try
        {
            var result = PullRequestEvent.Load(path);

            Assert.Equal(12345L, result.RunId);
            Assert.Equal(7, result.Number);
            Assert.Equal("main", result.BaseRef);
            Assert.Equal("## Details\nApproved", result.Body);
            Assert.Equal(new[] { "breaking-change-approved", "api" }, result.Labels);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [MemberData(nameof(MalformedRequiredFieldCases))]
    public void Load_ThrowsInvalidDataException_ForMalformedRequiredFields(
        string json,
        string expectedMessage)
    {
        using var scope = new EnvironmentVariableScope("GITHUB_RUN_ID", "12345");
        var path = WriteEvent(json);

        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => PullRequestEvent.Load(path));
            Assert.Contains(expectedMessage, ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static TheoryData<string, string> MalformedRequiredFieldCases => new()
    {
        {
            """
            {
              "pull_request": null
            }
            """,
            "pull_request"
        },
        {
            """
            {
              "pull_request": {
                "labels": null,
                "number": 7,
                "base": { "ref": "main" }
              }
            }
            """,
            "labels"
        },
        {
            """
            {
              "pull_request": {
                "labels": [
                  { "name": null }
                ],
                "number": 7,
                "base": { "ref": "main" }
              }
            }
            """,
            "labels[].name"
        },
        {
            """
            {
              "pull_request": {
                "labels": [
                  { "name": "breaking-change-approved" }
                ],
                "number": "seven",
                "base": { "ref": "main" }
              }
            }
            """,
            "number"
        },
        {
            """
            {
              "pull_request": {
                "labels": [
                  { "name": "breaking-change-approved" }
                ],
                "number": 7,
                "base": null
              }
            }
            """,
            "base"
        },
        {
            """
            {
              "pull_request": {
                "labels": [
                  { "name": "breaking-change-approved" }
                ],
                "number": 7,
                "base": { }
              }
            }
            """,
            "base.ref"
        },
    };

    private static string WriteEvent(string json)
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"pull-request-event-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previous);
        }
    }
}
