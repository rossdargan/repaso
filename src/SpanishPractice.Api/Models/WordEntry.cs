namespace SpanishPractice.Api.Models;

public class WordEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VocabularyImportId { get; set; }
    public VocabularyImport VocabularyImport { get; set; } = null!;

    public string? Pronunciation { get; set; }
    public string? Comment { get; set; }
    public bool AllowReverse { get; set; } = true;
    public GenderType Gender { get; set; }
    public NumberType Number { get; set; }
    public StateType State { get; set; }

    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public WordProgress? Progress { get; set; }
    public ICollection<Attempt> Attempts { get; set; } = new List<Attempt>();
    public ICollection<WordVariant> Variants { get; set; } = new List<WordVariant>();
    public ICollection<WordExample> Examples { get; set; } = new List<WordExample>();
}
