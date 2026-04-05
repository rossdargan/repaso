using Microsoft.EntityFrameworkCore;
using SpanishPractice.Api.Contracts;
using SpanishPractice.Api.Data;
using SpanishPractice.Api.Models;

namespace SpanishPractice.Api.Services;

public class QuestionSelectorService(AppDbContext dbContext)
{
    private const int RecentHistorySize = 7;
    private static readonly Queue<Guid> RecentWordIds = new();
    private static readonly Lock RecentWordIdsLock = new();

    public async Task<QuizQuestionResponse?> GetNextQuestionAsync(Guid[]? categoryIds, CancellationToken cancellationToken)
    {
        var query = dbContext.WordEntries
            .Include(x => x.Progress)
            .Include(x => x.Category)
            .Include(x => x.Variants)
            .Include(x => x.Examples)
            .AsQueryable();

        var words = await query.ToListAsync(cancellationToken);

        if (categoryIds is { Length: > 0 })
        {
            var ids = categoryIds.Select(x => x.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            words = words.Where(x => ids.Contains(x.CategoryId.ToString())).ToList();
        }
        words = words
            .Where(x => x.Variants.Any(v => v.Language == AnswerLanguage.English) && x.Variants.Any(v => v.Language == AnswerLanguage.Spanish))
            .ToList();

        if (words.Count == 0)
        {
            return null;
        }

        var recentIds = GetRecentWordIds();
        var eligibleWords = words.Where(word => !recentIds.Contains(word.Id)).ToList();
        if (eligibleWords.Count == 0)
        {
            eligibleWords = words;
        }

        var candidates = eligibleWords
            .Select(word => new { Word = word, Score = CalculateWeight(word.Progress) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => (x.Score * 100) + Random.Shared.NextDouble())
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = eligibleWords
                .Select(word => new { Word = word, Score = Math.Max(0.1, CalculateWeightIgnoringRecency(word.Progress)) })
                .OrderByDescending(x => (x.Score * 100) + Random.Shared.NextDouble())
                .ToList();
        }

        var selected = candidates.First();
        RememberWord(selected.Word.Id);

        var promptLanguage = selected.Word.AllowReverse
            ? (Random.Shared.Next(0, 2) == 0 ? PromptLanguage.English : PromptLanguage.Spanish)
            : PromptLanguage.English;
        var promptText = GetPrimaryVariant(selected.Word, promptLanguage)?.Text ?? string.Empty;
        var placeholder = promptLanguage == PromptLanguage.English ? "Type the Spanish answer" : "Type the English answer";
        var directionLabel = promptLanguage == PromptLanguage.English ? "English → Spanish" : "Spanish → English";
        var progress = selected.Word.Progress;
        var pronunciation = promptLanguage == PromptLanguage.Spanish ? selected.Word.Pronunciation : null;

        return new QuizQuestionResponse(
            selected.Word.Id,
            promptLanguage,
            promptText,
            placeholder,
            directionLabel,
            selected.Score,
            progress?.AttemptsCount ?? 0,
            progress?.WrongCount ?? 0,
            progress?.CurrentStreak ?? 0,
            progress?.EnglishToSpanishStreak ?? 0,
            progress?.SpanishToEnglishStreak ?? 0,
            IsReliablyKnown(progress),
            selected.Word.CategoryId,
            selected.Word.Category?.Name ?? string.Empty,
            pronunciation,
            (int)selected.Word.Gender,
            (int)selected.Word.Number,
            (int)selected.Word.State,
            selected.Word.Examples.OrderBy(x => x.SortOrder).Select(x => new ExampleSentenceResponse(x.Id, x.SpanishText, x.EnglishText, x.SortOrder)).ToList());
    }

    private static WordVariant? GetPrimaryVariant(WordEntry word, PromptLanguage promptLanguage)
    {
        var language = promptLanguage == PromptLanguage.English ? AnswerLanguage.English : AnswerLanguage.Spanish;
        return word.Variants.Where(x => x.Language == language).OrderBy(x => x.SortOrder).FirstOrDefault();
    }

    private static bool IsReliablyKnown(WordProgress? progress) => progress is not null && progress.EnglishToSpanishStreak >= 3 && progress.SpanishToEnglishStreak >= 1;
    private static HashSet<Guid> GetRecentWordIds() { lock (RecentWordIdsLock) { return RecentWordIds.ToHashSet(); } }
    private static void RememberWord(Guid wordId) { lock (RecentWordIdsLock) { var reordered = RecentWordIds.Where(id => id != wordId).ToList(); RecentWordIds.Clear(); foreach (var id in reordered) RecentWordIds.Enqueue(id); RecentWordIds.Enqueue(wordId); while (RecentWordIds.Count > RecentHistorySize) RecentWordIds.Dequeue(); } }
    private static double CalculateWeight(WordProgress? progress) { if (progress is null) return 5.0; var now = DateTimeOffset.UtcNow; var minutesSinceSeen = progress.LastSeenAtUtc is null ? 10_000 : (now - progress.LastSeenAtUtc.Value).TotalMinutes; var recentSeenCooldown = minutesSinceSeen switch { < 1 => 0.0, < 3 => 0.03, < 8 => 0.08, < 20 => 0.25, < 60 => 0.6, _ => 1.0 }; var wrongBias = progress.WrongCount * 2.0; var typoBias = progress.TypoSavedCount * 1.0; var streakRelief = Math.Min(progress.CurrentStreak * 0.9, 3.5); var unseenBonus = progress.AttemptsCount == 0 ? 2.0 : 0.0; var staleBonus = progress.LastSeenAtUtc is null ? 1.5 : Math.Min((now - progress.LastSeenAtUtc.Value).TotalHours / 8.0, 2.5); var recentWrongBonus = progress.LastWrongAtUtc is null ? 0.0 : Math.Max(0.0, 2.0 - ((now - progress.LastWrongAtUtc.Value).TotalMinutes / 30.0)); var baseScore = 1.0 + wrongBias + typoBias + unseenBonus + staleBonus + recentWrongBonus - streakRelief; return Math.Max(0.0, baseScore * recentSeenCooldown); }
    private static double CalculateWeightIgnoringRecency(WordProgress? progress) { if (progress is null) return 5.0; return Math.Max(1.0, 1.0 + (progress.WrongCount * 2.0) + (progress.TypoSavedCount * 1.0) - Math.Min(progress.CurrentStreak * 0.9, 3.5)); }
}
