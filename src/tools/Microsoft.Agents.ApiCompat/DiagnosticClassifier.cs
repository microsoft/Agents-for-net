namespace Microsoft.Agents.ApiCompat;

public static class DiagnosticClassifier
{
    public const string ApiCompatNoWarn = "CP0011;CP0013";

    private static readonly IReadOnlyDictionary<string, CompatibilityCategory> BaselineCategories =
        new Dictionary<string, CompatibilityCategory>(StringComparer.Ordinal)
        {
            ["CP0001"] = CompatibilityCategory.SourceAndBinary,
            ["CP0002"] = CompatibilityCategory.SourceAndBinary,
            ["CP0003"] = CompatibilityCategory.Binary,
            ["CP0004"] = CompatibilityCategory.SourceAndBinary,
            ["CP0005"] = CompatibilityCategory.SourceAndBinary,
            ["CP0006"] = CompatibilityCategory.SourceAndBinary,
            ["CP0007"] = CompatibilityCategory.SourceAndBinary,
            ["CP0008"] = CompatibilityCategory.SourceAndBinary,
            ["CP0009"] = CompatibilityCategory.SourceAndBinary,
            ["CP0010"] = CompatibilityCategory.Binary,
            ["CP0012"] = CompatibilityCategory.SourceAndBinary,
            ["CP0017"] = CompatibilityCategory.Source,
            ["CP0018"] = CompatibilityCategory.SourceAndBinary,
            ["CP0019"] = CompatibilityCategory.SourceAndBinary,
            ["PKV001"] = CompatibilityCategory.SourceAndBinary,
            ["PKV002"] = CompatibilityCategory.Binary,
            ["PKV003"] = CompatibilityCategory.Binary,
            ["PKV004"] = CompatibilityCategory.Binary,
            ["PKV005"] = CompatibilityCategory.Binary,
            ["PKV006"] = CompatibilityCategory.SourceAndBinary,
            ["PKV007"] = CompatibilityCategory.Binary,
        };

    public static Classification Classify(string diagnosticId, ApiDifferenceDirection direction)
    {
        if (direction == ApiDifferenceDirection.CandidateAddition &&
            diagnosticId is "CP0001" or "CP0002" or "CP0020")
        {
            return new(CompatibilityCategory.PotentialSourceRisk, FindingSeverity.Warning);
        }

        if (!BaselineCategories.TryGetValue(diagnosticId, out var category))
        {
            throw new InvalidDataException($"Unsupported ApiCompat diagnostic '{diagnosticId}'.");
        }

        return new(category, FindingSeverity.Blocking);
    }
}
