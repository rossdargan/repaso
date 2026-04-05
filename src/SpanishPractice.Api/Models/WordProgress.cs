namespace SpanishPractice.Api.Models;

public class WordProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WordEntryId { get; set; }
    public WordEntry? WordEntry { get; set; }

    public int ExactCorrectCount { get; set; }
    public int TypoSavedCount { get; set; }
    public int WrongCount { get; set; }
    public int CurrentStreak { get; set; }
    public int AttemptsCount { get; set; }
    public double PriorityScore { get; set; } = 1.0;
    public DateTimeOffset? LastSeenAtUtc { get; set; }
    public DateTimeOffset? LastWrongAtUtc { get; set; }
    public DateTimeOffset? LastExactAtUtc { get; set; }
    public int EnglishToSpanishStreak { get; set; }
    public int SpanishToEnglishStreak { get; set; }
    public int EnglishToSpanishCorrectCount { get; set; }
    public int SpanishToEnglishCorrectCount { get; set; }
}
