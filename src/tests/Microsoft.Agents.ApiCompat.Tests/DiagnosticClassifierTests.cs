using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public class DiagnosticClassifierTests
{
    [Fact]
    public void Classify_ParameterRename_IsSourceOnly()
    {
        var result = DiagnosticClassifier.Classify("CP0017", ApiDifferenceDirection.BaselineToCandidate);

        Assert.Equal(CompatibilityCategory.Source, result.Category);
        Assert.Equal(FindingSeverity.Blocking, result.Severity);
    }
}
