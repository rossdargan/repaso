namespace SpanishPractice.Api.Models;

public class WordVariant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WordEntryId { get; set; }
    public WordEntry WordEntry { get; set; } = null!;
    public AnswerLanguage Language { get; set; }
    public string Text { get; set; } = string.Empty;
    public string NormalizedText { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
