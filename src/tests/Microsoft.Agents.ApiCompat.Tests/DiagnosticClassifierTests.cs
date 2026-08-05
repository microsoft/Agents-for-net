using Microsoft.Agents.ApiCompat;
using Xunit;

namespace Microsoft.Agents.ApiCompat.Tests;

public class DiagnosticClassifierTests
{
    [Fact]
    public void ApiCompatNoWarn_HasPinnedLiteral()
    {
        Assert.Equal("CP0011;CP0013", DiagnosticClassifier.ApiCompatNoWarn);
    }

    [Theory]
    [InlineData("CP0017", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Source, FindingSeverity.Blocking)]
    [InlineData("CP0003", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
    [InlineData("CP0010", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
    [InlineData("PKV002", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
    [InlineData("PKV003", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
    [InlineData("PKV004", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
    [InlineData("PKV005", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
    [InlineData("PKV007", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.Binary, FindingSeverity.Blocking)]
    [InlineData("CP0001", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0002", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0004", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0005", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0006", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0007", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0008", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0009", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0012", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0018", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0019", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("PKV001", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("PKV006", ApiDifferenceDirection.BaselineToCandidate, CompatibilityCategory.SourceAndBinary, FindingSeverity.Blocking)]
    [InlineData("CP0001", ApiDifferenceDirection.CandidateAddition, CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning)]
    [InlineData("CP0002", ApiDifferenceDirection.CandidateAddition, CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning)]
    [InlineData("CP0020", ApiDifferenceDirection.CandidateAddition, CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning)]
    public void Classify_KnownDiagnostic_ReturnsPolicy(
        string id,
        ApiDifferenceDirection direction,
        CompatibilityCategory category,
        FindingSeverity severity)
    {
        Assert.Equal(new Classification(category, severity), DiagnosticClassifier.Classify(id, direction));
    }

    [Fact]
    public void Classify_UnknownDiagnostic_Throws()
    {
        Assert.Throws<InvalidDataException>(
            () => DiagnosticClassifier.Classify("CP9999", ApiDifferenceDirection.BaselineToCandidate));
    }

    [Fact]
    public void Classify_Cp0020_BaselineToCandidate_Throws()
    {
        var exception = Assert.Throws<InvalidDataException>(
            () => DiagnosticClassifier.Classify("CP0020", ApiDifferenceDirection.BaselineToCandidate));

        Assert.Contains("CP0020", exception.Message, StringComparison.Ordinal);
    }
}
