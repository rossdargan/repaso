using SpanishPractice.Api.Models;

namespace SpanishPractice.Api.Contracts;

public record QuizQuestionResponse(
    Guid WordId,
    PromptLanguage PromptLanguage,
    string PromptText,
    string Placeholder,
    string DirectionLabel,
    double PriorityScore,
    int Attempts,
    int WrongCount,
    int CurrentStreak,
    int EnglishToSpanishStreak,
    int SpanishToEnglishStreak,
    bool IsReliablyKnown,
    Guid CategoryId,
    string CategoryName,
    string? Pronunciation,
    int Gender,
    int Number,
    int State,
    IReadOnlyList<ExampleSentenceResponse> Examples);

public class SubmitAttemptRequest
{
    public Guid WordId { get; set; }
    public PromptLanguage PromptLanguage { get; set; }
    public string SubmittedAnswer { get; set; } = string.Empty;
    public int Gender { get; set; }
    public int Number { get; set; }
    public int State { get; set; }
    public bool? AcceptCloseMatch { get; set; }
}

public record SubmitAttemptResponse(
    bool IsCorrect,
    bool IsCloseMatch,
    bool NeedsCloseMatchDecision,
    string ExpectedAnswer,
    IReadOnlyList<string> AcceptedAnswers,
    string SubmittedAnswer,
    int EditDistance,
    AttemptResultType? ResultType,
    string Message,
    ProgressSummaryResponse Summary,
    int CurrentStreak,
    string Encouragement,
    int Gender,
    int Number,
    int State,
    string? Comment,
    string? Pronunciation,
    IReadOnlyList<ExampleSentenceResponse> Examples);

public record ProgressSummaryResponse(int TotalWords, int AttemptsToday, int ExactCorrect, int TypoSaved, int Wrong, int CurrentStreak, int ReliablyKnownCount, IReadOnlyList<WeakWordResponse> WeakWords);

public record WeakWordResponse(Guid WordId, string English, string Spanish, double PriorityScore, int WrongCount, int AttemptsCount, DateTimeOffset? LastWrongAtUtc);
