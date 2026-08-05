using System.Text.RegularExpressions;

namespace Microsoft.Agents.ApiCompat;

public static partial class OverridePolicy
{
    public const string ApprovalLabel = "breaking-change-approved";
    private const string Heading = "Breaking change justification";

    public static OverrideResult Evaluate(IReadOnlyCollection<string> labels, string? body)
    {
        var hasLabel = labels.Contains(ApprovalLabel, StringComparer.OrdinalIgnoreCase);
        var justification = ExtractJustification(body ?? string.Empty);

        if (!hasLabel)
        {
            return new(false, justification, $"Missing '{ApprovalLabel}' label.");
        }

        if (string.IsNullOrWhiteSpace(justification))
        {
            return new(false, null, $"Missing non-empty '## {Heading}' section.");
        }

        return new(true, justification, "Approved label and justification are present.");
    }

    private static string? ExtractJustification(string body)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line =>
            string.Equals(line.Trim(), $"## {Heading}", StringComparison.OrdinalIgnoreCase));
        if (start < 0)
        {
            return null;
        }

        var content = lines.Skip(start + 1)
            .TakeWhile(line => !HeadingRegex().IsMatch(line))
            .ToArray();
        var visible = HtmlCommentRegex().Replace(string.Join("\n", content), string.Empty).Trim();
        return visible.Length == 0 ? null : visible;
    }

    [GeneratedRegex(@"^#{1,2}\s+", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentRegex();
}
