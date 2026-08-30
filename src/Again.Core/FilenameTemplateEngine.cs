using System.Text.RegularExpressions;

namespace Again.Core;

public static partial class FilenameTemplateEngine
{
    private const string StemToken = "{stem}";
    private const string NumberToken = "{number}";

    public static string Infer(string sourceStem, string outputStem)
    {
        if (string.Equals(sourceStem, outputStem, StringComparison.OrdinalIgnoreCase))
            return StemToken;

        var directIndex = outputStem.IndexOf(sourceStem, StringComparison.OrdinalIgnoreCase);
        if (directIndex >= 0)
        {
            return outputStem[..directIndex] + StemToken + outputStem[(directIndex + sourceStem.Length)..];
        }

        var sourceMatch = TrailingNumberRegex().Match(sourceStem);
        var outputMatch = TrailingNumberRegex().Match(outputStem);
        if (sourceMatch.Success && outputMatch.Success &&
            string.Equals(sourceMatch.Groups[2].Value, outputMatch.Groups[2].Value, StringComparison.Ordinal))
        {
            var prefix = outputMatch.Groups[1].Value;
            return prefix + NumberToken;
        }

        // If the demonstrated output is explicitly numbered 1, treat that as the
        // first item of a new output sequence. This covers demonstrations such as
        // Screenshot (261) -> test1, where the user's intent is test2, test3, ...
        if (outputMatch.Success && int.TryParse(outputMatch.Groups[2].Value, out var outputNumber) && outputNumber == 1)
        {
            var prefix = outputMatch.Groups[1].Value;
            var width = outputMatch.Groups[2].Value.Length;
            return prefix + (width > 1 ? $"{{sequence:{width}}}" : "{sequence}");
        }

        return StemToken;
    }

    public static string? ExtractTrailingNumber(string stem)
    {
        var match = TrailingNumberRegex().Match(stem);
        return match.Success ? match.Groups[2].Value : null;
    }

    public static string Apply(string template, string stem, string? trailingNumber, int sequenceNumber = 1)
    {
        var result = template.Replace(StemToken, stem, StringComparison.Ordinal);
        result = result.Replace(NumberToken, trailingNumber ?? stem, StringComparison.Ordinal);
        result = SequenceTokenRegex().Replace(result, match =>
        {
            var widthText = match.Groups[1].Value;
            if (int.TryParse(widthText, out var width) && width > 0)
                return sequenceNumber.ToString($"D{Math.Min(width, 12)}");
            return sequenceNumber.ToString();
        });
        return SanitizeFileName(result);
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "Again Output" : name.Trim();
    }

    [GeneratedRegex("^(.*?)(\\d+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNumberRegex();

    [GeneratedRegex("\\{sequence(?::(\\d+))?\\}", RegexOptions.CultureInvariant)]
    private static partial Regex SequenceTokenRegex();
}
