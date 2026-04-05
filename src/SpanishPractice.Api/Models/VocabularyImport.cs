namespace SpanishPractice.Api.Models;

public class VocabularyImport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ImportStatus Status { get; set; } = ImportStatus.Pending;
    public int ImportedCount { get; set; }
    public string? Notes { get; set; }

    public ICollection<WordEntry> Words { get; set; } = new List<WordEntry>();
}
