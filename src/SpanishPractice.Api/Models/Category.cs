namespace SpanishPractice.Api.Models;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<WordEntry> WordEntries { get; set; } = new List<WordEntry>();
}
