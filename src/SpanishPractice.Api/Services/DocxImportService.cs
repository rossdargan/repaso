using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SpanishPractice.Api.Contracts;

namespace SpanishPractice.Api.Services;

public class DocxImportService
{
    public IReadOnlyList<ParsedWordPair> Parse(string filePath)
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
                    pairs.Add(new ParsedWordPair(english, spanish, pronunciation, comment));
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
                return new ParsedWordPair(left, right, null, null);
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
            .GroupBy(x => new { English = x.English.Trim().ToLowerInvariant(), Spanish = x.Spanish.Trim().ToLowerInvariant() })
            .Select(x => x.First())
            .ToList();
    }
}
