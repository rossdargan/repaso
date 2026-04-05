using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SpanishPractice.Api.Contracts;
using SpanishPractice.Api.Models;

namespace SpanishPractice.Api.Services;

public class DocxImportService
{
    private static readonly string[] CsvHeaders =
    [
        "category",
        "english_prompt",
        "additional_english_answers",
        "spanish_prompt",
        "additional_spanish_answers",
        "gender",
        "number",
        "state",
        "pronunciation",
        "comment",
        "examples",
    ];

    public IReadOnlyList<ParsedWordPair> Parse(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ParseCsv(filePath)
            : ParseDocx(filePath);
    }

    private static IReadOnlyList<ParsedWordPair> ParseDocx(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, false);
        var body = document.MainDocumentPart?.Document.Body;

        if (body is null)
        {
            return [];
        }

        var pairs = new List<ParsedWordPair>();

        foreach (var table in body.Descendants<Table>())
        {
            foreach (var row in table.Descendants<TableRow>())
            {
                var cells = row.Descendants<TableCell>()
                    .Select(cell => string.Join(" ", cell.Descendants<Text>().Select(t => t.Text)).Trim())
                    .Where(text => !string.IsNullOrWhiteSpace(text))
                    .ToList();

                if (cells.Count >= 2 && !LooksLikeHeader(cells))
                {
                    var english = cells.ElementAtOrDefault(0) ?? string.Empty;
                    var spanish = cells.ElementAtOrDefault(1) ?? string.Empty;
                    var pronunciation = cells.ElementAtOrDefault(2);
                    var comment = cells.ElementAtOrDefault(3);
                    pairs.Add(new ParsedWordPair(
                        "Imported",
                        english,
                        [],
                        spanish,
                        [],
                        (int)GenderType.NotApplicable,
                        (int)NumberType.NotApplicable,
                        (int)StateType.NotApplicable,
                        pronunciation,
                        comment,
                        []));
                }
            }
        }

        if (pairs.Count > 0)
        {
            return Deduplicate(pairs);
        }

        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            var text = string.Join(" ", paragraph.Descendants<Text>().Select(t => t.Text)).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var split = SplitLine(text);
            if (split is not null)
            {
                pairs.Add(split);
            }
        }

        return Deduplicate(pairs);
    }

    private static IReadOnlyList<ParsedWordPair> ParseCsv(string filePath)
    {
        var text = File.ReadAllText(filePath);
        var rows = ParseCsvRows(text);
        if (rows.Count == 0)
        {
            return [];
        }

        var header = rows[0].Select(NormalizeHeader).ToList();
        if (header.Count != CsvHeaders.Length || !CsvHeaders.SequenceEqual(header, StringComparer.Ordinal))
        {
            return [];
        }

        var pairs = new List<ParsedWordPair>();
        foreach (var row in rows.Skip(1))
        {
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var padded = row.Concat(Enumerable.Repeat(string.Empty, CsvHeaders.Length)).Take(CsvHeaders.Length).ToList();
            var category = padded[0].Trim();
            var englishPrompt = padded[1].Trim();
            var additionalEnglish = SplitPipeSeparated(padded[2]);
            var spanishPrompt = padded[3].Trim();
            var additionalSpanish = SplitPipeSeparated(padded[4]);
            var gender = ParseEnumValue<GenderType>(padded[5]);
            var number = ParseEnumValue<NumberType>(padded[6]);
            var state = ParseEnumValue<StateType>(padded[7]);
            var pronunciation = NullIfWhitespace(padded[8]);
            var comment = NullIfWhitespace(padded[9]);
            var examples = ParseExamples(padded[10]);

            if (string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(englishPrompt) || string.IsNullOrWhiteSpace(spanishPrompt))
            {
                continue;
            }

            pairs.Add(new ParsedWordPair(
                category,
                englishPrompt,
                additionalEnglish,
                spanishPrompt,
                additionalSpanish,
                (int)gender,
                (int)number,
                (int)state,
                pronunciation,
                comment,
                examples));
        }

        return Deduplicate(pairs);
    }

    private static ParsedWordPair? SplitLine(string line)
    {
        var separators = new[] { " - ", " – ", " — ", ":", "\t" };
        foreach (var separator in separators)
        {
            var index = line.IndexOf(separator, StringComparison.Ordinal);
            if (index <= 0 || index >= line.Length - separator.Length)
            {
                continue;
            }

            var left = line[..index].Trim();
            var right = line[(index + separator.Length)..].Trim();
            if (!string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right))
            {
                return new ParsedWordPair(
                    "Imported",
                    left,
                    [],
                    right,
                    [],
                    (int)GenderType.NotApplicable,
                    (int)NumberType.NotApplicable,
                    (int)StateType.NotApplicable,
                    null,
                    null,
                    []);
            }
        }

        return null;
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> cells)
    {
        var combined = string.Join(" ", cells).ToLowerInvariant();
        return combined.Contains("english") && combined.Contains("spanish");
    }

    private static IReadOnlyList<ParsedWordPair> Deduplicate(IEnumerable<ParsedWordPair> pairs)
    {
        return pairs
            .GroupBy(x => new
            {
                Category = x.Category.Trim().ToLowerInvariant(),
                English = x.English.Trim().ToLowerInvariant(),
                Spanish = x.Spanish.Trim().ToLowerInvariant(),
            })
            .Select(x => x.First())
            .ToList();
    }

    private static List<List<string>> ParseCsvRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }

                continue;
            }

            if (ch == '"')
            {
                inQuotes = true;
                continue;
            }

            if (ch == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
                continue;
            }

            if (ch == '\r')
            {
                continue;
            }

            if (ch == '\n')
            {
                row.Add(cell.ToString());
                cell.Clear();
                rows.Add(row);
                row = new List<string>();
                continue;
            }

            cell.Append(ch);
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static string NormalizeHeader(string value) => value.Trim().ToLowerInvariant();

    private static List<string> SplitPipeSeparated(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

    private static TEnum ParseEnumValue<TEnum>(string? value)
        where TEnum : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value.Trim(), true, out var parsed))
        {
            return parsed;
        }

        return default;
    }

    private static string? NullIfWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<ExampleSentenceInput> ParseExamples(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(example => example.Split("=>", 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && (!string.IsNullOrWhiteSpace(parts[0]) || !string.IsNullOrWhiteSpace(parts[1])))
            .Select(parts => new ExampleSentenceInput(parts[0], parts[1]))
            .ToList();
    }
}
