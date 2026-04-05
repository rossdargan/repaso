using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpanishPractice.Api.Data;
using SpanishPractice.Api.Models;
using SpanishPractice.Api.Services;

namespace Repaso.Api.Tests;

public class QuestionSelectorServiceTests
{
    [Fact]
    public async Task GetNextQuestionAsync_PrefersWordsWithHigherPrioritySignals()
    {
        await using var db = CreateDb();
        var general = new Category { Name = "General" };
        db.Categories.Add(general);

        var easyWord = CreateWord(general, "cat", "gato");
        easyWord.Progress = new WordProgress
        {
            WordEntryId = easyWord.Id,
            AttemptsCount = 5,
            ExactCorrectCount = 5,
            CurrentStreak = 5,
            PriorityScore = 1,
            LastSeenAtUtc = DateTimeOffset.UtcNow,
        };

        var hardWord = CreateWord(general, "window", "ventana");
        hardWord.Progress = new WordProgress
        {
            WordEntryId = hardWord.Id,
            AttemptsCount = 6,
            WrongCount = 4,
            TypoSavedCount = 1,
            PriorityScore = 10,
            LastSeenAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
            LastWrongAtUtc = DateTimeOffset.UtcNow,
        };

        db.WordEntries.AddRange(easyWord, hardWord);
        await db.SaveChangesAsync();

        var selector = new QuestionSelectorService(db);
        var seenHardWord = false;

        for (var i = 0; i < 20; i++)
        {
            var question = await selector.GetNextQuestionAsync(null, CancellationToken.None);
            if (question?.WordId == hardWord.Id)
            {
                seenHardWord = true;
                break;
            }
        }

        seenHardWord.Should().BeTrue();
    }

    [Fact]
    public async Task GetNextQuestionAsync_FiltersBySelectedCategories()
    {
        await using var db = CreateDb();
        var animals = new Category { Name = "Animals" };
        var travel = new Category { Name = "Travel" };
        db.Categories.AddRange(animals, travel);

        var animalWord = CreateWord(animals, "dog", "perro");
        var travelWord = CreateWord(travel, "station", "estación");

        db.WordEntries.AddRange(animalWord, travelWord);
        await db.SaveChangesAsync();

        var selector = new QuestionSelectorService(db);
        var question = await selector.GetNextQuestionAsync([travel.Id], CancellationToken.None);

        question.Should().NotBeNull();
        question!.WordId.Should().Be(travelWord.Id);
        question.CategoryId.Should().Be(travel.Id);
        question.CategoryName.Should().Be("Travel");
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static WordEntry CreateWord(Category category, string english, string spanish)
    {
        var manualImport = new VocabularyImport
        {
            FileName = "test-import",
            OriginalFileName = "test-import",
            Status = ImportStatus.Completed,
        };

        return new WordEntry
        {
            VocabularyImport = manualImport,
            Category = category,
            CategoryId = category.Id,
            AllowReverse = true,
            Gender = GenderType.NotApplicable,
            Number = NumberType.NotApplicable,
            State = StateType.NotApplicable,
            Variants =
            [
                new WordVariant
                {
                    Language = AnswerLanguage.English,
                    Text = english,
                    NormalizedText = english,
                    SortOrder = 0,
                },
                new WordVariant
                {
                    Language = AnswerLanguage.Spanish,
                    Text = spanish,
                    NormalizedText = spanish,
                    SortOrder = 0,
                },
            ],
        };
    }
}
