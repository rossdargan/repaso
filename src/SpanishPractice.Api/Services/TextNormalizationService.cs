using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpanishPractice.Api.Services;

public class TextNormalizationService
{
    private static readonly Regex MultiSpaceRegex = new("\\s+", RegexOptions.Compiled);
    private static readonly Regex KeepLettersNumbersRegex = new("[^\\p{L}\\p{N}\\s]", RegexOptions.Compiled);

    public string Normalize(string? input, bool removeAccents = true)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var value = input.Trim().ToLowerInvariant();
        value = KeepLettersNumbersRegex.Replace(value, " ");

        if (removeAccents)
        {
            value = RemoveDiacritics(value);
        }

        value = MultiSpaceRegex.Replace(value, " ").Trim();
        return value;
    }

    public string RemoveDiacritics(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();

        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
