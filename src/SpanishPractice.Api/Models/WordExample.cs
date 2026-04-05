namespace SpanishPractice.Api.Models;

public class WordExample
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WordEntryId { get; set; }
    public WordEntry WordEntry { get; set; } = null!;

    public string SpanishText { get; set; } = string.Empty;
    public string EnglishText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
