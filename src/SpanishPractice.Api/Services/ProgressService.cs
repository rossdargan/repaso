using Microsoft.EntityFrameworkCore;
using SpanishPractice.Api.Contracts;
using SpanishPractice.Api.Data;
using SpanishPractice.Api.Models;

namespace SpanishPractice.Api.Services;

public class ProgressService(AppDbContext dbContext)
{
    public async Task<int> RecordAttemptAsync(WordEntry word, PromptLanguage promptLanguage, string promptText, string expectedAnswer, string submittedAnswer, string normalizedSubmittedAnswer, AttemptResultType resultType, bool wasCloseMatch, int editDistance, CancellationToken cancellationToken)
    {
        var progress = word.Progress ?? await dbContext.WordProgressEntries.FirstOrDefaultAsync(x => x.WordEntryId == word.Id, cancellationToken) ?? new WordProgress { WordEntryId = word.Id };

        progress.AttemptsCount += 1;
        progress.LastSeenAtUtc = DateTimeOffset.UtcNow;

        var isEnglishToSpanish = promptLanguage == PromptLanguage.English;

        switch (resultType)
        {
            case AttemptResultType.Exact:
                progress.ExactCorrectCount += 1;
                progress.CurrentStreak += 1;
                progress.LastExactAtUtc = DateTimeOffset.UtcNow;
                if (isEnglishToSpanish)
                {
                    progress.EnglishToSpanishStreak += 1;
                    progress.EnglishToSpanishCorrectCount += 1;
                }
                else
                {
                    progress.SpanishToEnglishStreak += 1;
                    progress.SpanishToEnglishCorrectCount += 1;
                }
                break;
            case AttemptResultType.TypoSaved:
                progress.TypoSavedCount += 1;
                progress.CurrentStreak += 1;
                if (isEnglishToSpanish)
                {
                    progress.EnglishToSpanishStreak += 1;
                    progress.EnglishToSpanishCorrectCount += 1;
                }
                else
                {
                    progress.SpanishToEnglishStreak += 1;
                    progress.SpanishToEnglishCorrectCount += 1;
                }
                break;
            case AttemptResultType.Wrong:
                progress.WrongCount += 1;
                progress.CurrentStreak = 0;
                progress.LastWrongAtUtc = DateTimeOffset.UtcNow;
                if (isEnglishToSpanish)
                {
                    progress.EnglishToSpanishStreak = 0;
                }
                else
                {
                    progress.SpanishToEnglishStreak = 0;
                }
                break;
        }

        progress.PriorityScore = CalculatePriority(progress);

        if (word.Progress is null)
        {
            dbContext.WordProgressEntries.Add(progress);
            word.Progress = progress;
        }

        dbContext.Attempts.Add(new Attempt
        {
            WordEntryId = word.Id,
            PromptLanguage = promptLanguage,
            PromptText = promptText,
            ExpectedAnswer = expectedAnswer,
            SubmittedAnswer = submittedAnswer,
            NormalizedSubmittedAnswer = normalizedSubmittedAnswer,
            ResultType = resultType,
            WasCloseMatch = wasCloseMatch,
            EditDistance = editDistance,
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return progress.CurrentStreak;
    }

    public async Task<ProgressSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var todayStart = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var todayStartOffset = new DateTimeOffset(todayStart, TimeSpan.Zero);
        var allAttempts = await dbContext.Attempts.AsNoTracking().ToListAsync(cancellationToken);
        var attemptsToday = allAttempts.Count(x => x.CreatedAtUtc >= todayStartOffset);
        var exactCorrect = allAttempts.Count(x => x.ResultType == AttemptResultType.Exact);
        var typoSaved = allAttempts.Count(x => x.ResultType == AttemptResultType.TypoSaved);
        var wrong = allAttempts.Count(x => x.ResultType == AttemptResultType.Wrong);
        var progressEntries = await dbContext.WordProgressEntries.AsNoTracking().ToListAsync(cancellationToken);
        var currentStreak = allAttempts
            .OrderByDescending(x => x.CreatedAtUtc)
            .TakeWhile(x => x.ResultType != AttemptResultType.Wrong)
            .Count();
        var reliablyKnownCount = progressEntries.Count(x => x.EnglishToSpanishStreak >= 3 && x.SpanishToEnglishStreak >= 1);

        var weakWords = await dbContext.WordEntries
            .Include(x => x.Progress)
            .Include(x => x.Variants)
            .Where(x => x.Progress != null)
            .OrderByDescending(x => x.Progress!.PriorityScore)
            .ThenByDescending(x => x.Progress!.WrongCount)
            .Take(10)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var weakWordResponses = weakWords.Select(x => new WeakWordResponse(
            x.Id,
            x.Variants.Where(v => v.Language == AnswerLanguage.English).OrderBy(v => v.SortOrder).Select(v => v.Text).FirstOrDefault() ?? string.Empty,
            x.Variants.Where(v => v.Language == AnswerLanguage.Spanish).OrderBy(v => v.SortOrder).Select(v => v.Text).FirstOrDefault() ?? string.Empty,
            x.Progress!.PriorityScore,
            x.Progress!.WrongCount,
            x.Progress!.AttemptsCount,
            x.Progress!.LastWrongAtUtc)).ToList();

        var totalWords = await dbContext.WordEntries.CountAsync(cancellationToken);
        return new ProgressSummaryResponse(totalWords, attemptsToday, exactCorrect, typoSaved, wrong, currentStreak, reliablyKnownCount, weakWordResponses);
    }

    public string GetEncouragement(int streak) => streak switch { >= 15 => "¡Increíble!", >= 10 => "¡Impresionante!", >= 7 => "¡Muy bien!", >= 5 => "¡Sigue así!", >= 3 => "¡Buen trabajo!", >= 1 => "¡Bien hecho!", _ => "¡Vamos!" };
    private static double CalculatePriority(WordProgress progress) => Math.Max(1.0, 1.0 + (progress.WrongCount * 2.5) + (progress.TypoSavedCount * 1.2) - (Math.Min(progress.ExactCorrectCount, 5) * 0.4) - (Math.Min(progress.CurrentStreak, 4) * 0.6));
}
