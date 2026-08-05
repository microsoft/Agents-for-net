using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public class OverridePolicyTests
{
    [Theory]
    [InlineData(false, "## Breaking change justification\nIntentional removal.", false)]
    [InlineData(true, null, false)]
    [InlineData(true, "## Breaking change justification\n<!-- explain -->", false)]
    [InlineData(true, "## Breaking change justification\nIntentional removal.\n## Testing\nDone.", true)]
    public void Evaluate_RequiresLabelAndVisibleJustification(bool hasLabel, string? body, bool expected)
    {
        var labels = hasLabel ? new[] { OverridePolicy.ApprovalLabel } : Array.Empty<string>();
        Assert.Equal(expected, OverridePolicy.Evaluate(labels, body).IsValid);
    }
}
