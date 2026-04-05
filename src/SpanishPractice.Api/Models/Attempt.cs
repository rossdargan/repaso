namespace SpanishPractice.Api.Models;

public class Attempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WordEntryId { get; set; }
    public WordEntry? WordEntry { get; set; }

    public PromptLanguage PromptLanguage { get; set; }
    public string PromptText { get; set; } = string.Empty;
    public string ExpectedAnswer { get; set; } = string.Empty;
    public string SubmittedAnswer { get; set; } = string.Empty;
    public string NormalizedSubmittedAnswer { get; set; } = string.Empty;
    public AttemptResultType ResultType { get; set; }
    public bool WasCloseMatch { get; set; }
    public int EditDistance { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
