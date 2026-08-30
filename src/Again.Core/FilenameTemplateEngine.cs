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

        return StemToken;
    }

    public static string? ExtractTrailingNumber(string stem)
    {
        var match = TrailingNumberRegex().Match(stem);
        return match.Success ? match.Groups[2].Value : null;
    }

    public static string Apply(string template, string stem, string? trailingNumber)
    {
        var result = template.Replace(StemToken, stem, StringComparison.Ordinal);
        result = result.Replace(NumberToken, trailingNumber ?? stem, StringComparison.Ordinal);
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
}
