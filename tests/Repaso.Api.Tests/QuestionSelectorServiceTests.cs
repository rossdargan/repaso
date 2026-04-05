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
        await db.SaveChangesAsync();

        var easyWord = new WordEntry
        {
            English = "cat",
            Spanish = "gato",
            NormalizedEnglish = "cat",
            NormalizedSpanish = "gato",
            CategoryId = general.Id,
            Progress = new WordProgress
            {
                AttemptsCount = 5,
                ExactCorrectCount = 5,
                CurrentStreak = 5,
                PriorityScore = 1,
                LastSeenAtUtc = DateTimeOffset.UtcNow,
            },
        };

        var hardWord = new WordEntry
        {
            English = "window",
            Spanish = "ventana",
            NormalizedEnglish = "window",
            NormalizedSpanish = "ventana",
            CategoryId = general.Id,
            Progress = new WordProgress
            {
                AttemptsCount = 6,
                WrongCount = 4,
                TypoSavedCount = 1,
                PriorityScore = 10,
                LastSeenAtUtc = DateTimeOffset.UtcNow.AddDays(-2),
                LastWrongAtUtc = DateTimeOffset.UtcNow,
            },
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
        await db.SaveChangesAsync();

        var animalWord = new WordEntry
        {
            English = "dog",
            Spanish = "perro",
            NormalizedEnglish = "dog",
            NormalizedSpanish = "perro",
            CategoryId = animals.Id,
        };

        var travelWord = new WordEntry
        {
            English = "station",
            Spanish = "estación",
            NormalizedEnglish = "station",
            NormalizedSpanish = "estacion",
            CategoryId = travel.Id,
        };

        db.WordEntries.AddRange(animalWord, travelWord);
        await db.SaveChangesAsync();

        var selector = new QuestionSelectorService(db);
        var question = await selector.GetNextQuestionAsync(new[] { travel.Id }, CancellationToken.None);

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
}
