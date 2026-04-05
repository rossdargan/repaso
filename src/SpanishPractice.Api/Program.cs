using System.Text;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SpanishPractice.Api.Contracts;
using SpanishPractice.Api.Data;
using SpanishPractice.Api.Models;
using SpanishPractice.Api.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
});

builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 20 * 1024 * 1024;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<TextNormalizationService>();
builder.Services.AddSingleton<EditDistanceService>();
builder.Services.AddSingleton<CloseMatchService>();
builder.Services.AddSingleton<DocxImportService>();
builder.Services.AddScoped<QuestionSelectorService>();
builder.Services.AddScoped<ProgressService>();

var app = builder.Build();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
if (!string.IsNullOrWhiteSpace(dataSource))
{
    var fullDataPath = Path.IsPathRooted(dataSource)
        ? dataSource
        : Path.Combine(app.Environment.ContentRootPath, dataSource);

    var directory = Path.GetDirectoryName(fullDataPath);
    if (!string.IsNullOrWhiteSpace(directory))
    {
        Directory.CreateDirectory(directory);
    }
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await EnsureDatabaseSchemaAsync(db, CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { ok = true }));

app.MapGet("/api/categories", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var categories = await dbContext.Categories
        .AsNoTracking()
        .OrderBy(x => x.Name)
        .Select(x => new CategoryResponse(x.Id, x.Name))
        .ToListAsync(cancellationToken);

    return Results.Ok(categories);
});

app.MapPost("/api/categories", async (CreateCategoryRequest request, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest(new { message = "Category name is required." });
    }

    var existing = await dbContext.Categories.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
    if (existing is not null)
    {
        return Results.Ok(new CategoryResponse(existing.Id, existing.Name));
    }

    var category = new Category { Name = name };
    dbContext.Categories.Add(category);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(new CategoryResponse(category.Id, category.Name));
});

app.MapGet("/api/words/export", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var words = await dbContext.WordEntries
        .AsNoTracking()
        .Include(x => x.Category)
        .Include(x => x.Variants)
        .Include(x => x.Examples)
        .ToListAsync(cancellationToken);

    var csvBuilder = new StringBuilder();
    csvBuilder.AppendLine("EnglishAnswers,SpanishAnswers,Pronunciation,Comment,Gender,Number,State,Category");

    foreach (var word in words)
    {
        var englishAnswers = string.Join(" | ", word.Variants.Where(x => x.Language == AnswerLanguage.English).OrderBy(x => x.SortOrder).Select(x => x.Text));
        var spanishAnswers = string.Join(" | ", word.Variants.Where(x => x.Language == AnswerLanguage.Spanish).OrderBy(x => x.SortOrder).Select(x => x.Text));
        csvBuilder.AppendLine($"\"{EscapeCsv(englishAnswers)}\",\"{EscapeCsv(spanishAnswers)}\",\"{EscapeCsv(word.Pronunciation)}\",\"{EscapeCsv(word.Comment)}\",\"{word.Gender}\",\"{word.Number}\",\"{word.State}\",\"{EscapeCsv(word.Category.Name)}\"");
    }

    var bytes = Encoding.UTF8.GetBytes(csvBuilder.ToString());
    return Results.File(bytes, "text/csv; charset=utf-8", "repaso-word-list.csv");
});

app.MapGet("/api/words", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var words = await dbContext.WordEntries
        .AsNoTracking()
        .Include(x => x.Category)
        .Include(x => x.Variants)
        .Include(x => x.Examples)
        .ToListAsync(cancellationToken);

    return Results.Ok(words.Select(MapWordResponse));
});

app.MapPost("/api/words", async (SaveWordRequest request, AppDbContext dbContext, TextNormalizationService normalizer, CancellationToken cancellationToken) =>
{
    if (!Guid.TryParse(request.CategoryId, out var parsedCategoryId))
    {
        return Results.BadRequest(new { message = "Pick a category first." });
    }

    var importId = await EnsureManualImportAsync(dbContext, cancellationToken);
    var category = await GetCategoryAsync(parsedCategoryId, dbContext, cancellationToken);
    var englishAnswers = CleanAnswers(new[] { request.EnglishPrompt }.Concat(request.AdditionalEnglishAnswers));
    var spanishAnswers = CleanAnswers(new[] { request.SpanishPrompt }.Concat(request.AdditionalSpanishAnswers));

    if (englishAnswers.Count == 0 || spanishAnswers.Count == 0)
    {
        return Results.BadRequest(new { message = "At least one English and one Spanish answer are required." });
    }

    var word = new WordEntry
    {
        VocabularyImportId = importId,
        CategoryId = category.Id,
        Pronunciation = NullIfWhitespace(request.Pronunciation),
        Comment = NullIfWhitespace(request.Comment),
        AllowReverse = request.AllowReverse,
        Gender = (GenderType)request.Gender,
        Number = (NumberType)request.Number,
        State = (StateType)request.State,
        Variants = BuildVariants(englishAnswers, spanishAnswers, normalizer),
        Examples = BuildExamples(request.Examples),
    };

    dbContext.WordEntries.Add(word);
    await dbContext.SaveChangesAsync(cancellationToken);

    await dbContext.Entry(word).Reference(x => x.Category).LoadAsync(cancellationToken);
    await dbContext.Entry(word).Collection(x => x.Variants).LoadAsync(cancellationToken);
    await dbContext.Entry(word).Collection(x => x.Examples).LoadAsync(cancellationToken);
    return Results.Ok(MapWordResponse(word));
});

app.MapPut("/api/words/{id:guid}", async (Guid id, SaveWordRequest request, AppDbContext dbContext, TextNormalizationService normalizer, CancellationToken cancellationToken) =>
{
    var word = await dbContext.WordEntries
        .Include(x => x.Category)
        .Include(x => x.Variants)
        .Include(x => x.Examples)
        .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    if (word is null)
    {
        return Results.NotFound(new { message = "Word not found." });
    }

    var englishAnswers = CleanAnswers(new[] { request.EnglishPrompt }.Concat(request.AdditionalEnglishAnswers));
    var spanishAnswers = CleanAnswers(new[] { request.SpanishPrompt }.Concat(request.AdditionalSpanishAnswers));
    if (englishAnswers.Count == 0 || spanishAnswers.Count == 0)
    {
        return Results.BadRequest(new { message = "At least one English and one Spanish answer are required." });
    }

    if (!Guid.TryParse(request.CategoryId, out var parsedCategoryId))
    {
        return Results.BadRequest(new { message = "Pick a category first." });
    }

    word.CategoryId = (await GetCategoryAsync(parsedCategoryId, dbContext, cancellationToken)).Id;
    word.Pronunciation = NullIfWhitespace(request.Pronunciation);
    word.Comment = NullIfWhitespace(request.Comment);
    word.AllowReverse = request.AllowReverse;
    word.Gender = (GenderType)request.Gender;
    word.Number = (NumberType)request.Number;
    word.State = (StateType)request.State;

    dbContext.WordVariants.RemoveRange(word.Variants.ToList());
    dbContext.WordExamples.RemoveRange(word.Examples.ToList());
    await dbContext.SaveChangesAsync(cancellationToken);

    var newVariants = BuildVariants(englishAnswers, spanishAnswers, normalizer);
    var newExamples = BuildExamples(request.Examples);
    foreach (var variant in newVariants)
    {
        variant.WordEntryId = word.Id;
        dbContext.WordVariants.Add(variant);
    }

    foreach (var example in newExamples)
    {
        example.WordEntryId = word.Id;
        dbContext.WordExamples.Add(example);
    }

    await dbContext.SaveChangesAsync(cancellationToken);
    await dbContext.Entry(word).Reference(x => x.Category).LoadAsync(cancellationToken);
    await dbContext.Entry(word).Collection(x => x.Variants).LoadAsync(cancellationToken);
    await dbContext.Entry(word).Collection(x => x.Examples).LoadAsync(cancellationToken);
    return Results.Ok(MapWordResponse(word));
});

app.MapDelete("/api/words/{id:guid}", async (Guid id, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    var word = await dbContext.WordEntries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    if (word is null)
    {
        return Results.NotFound(new { message = "Word not found." });
    }

    dbContext.WordEntries.Remove(word);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { deleted = true });
});

app.MapPost("/api/imports/preview", async (HttpRequest request, IOptions<AppOptions> options, DocxImportService importService, AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { message = "Expected multipart form upload." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files["file"];

    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { message = "Empty file." });
    }

    var extension = Path.GetExtension(file.FileName);
    if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { message = "Only .docx and .csv files are supported." });
    }

    var uploadsPath = Path.Combine(app.Environment.ContentRootPath, options.Value.UploadsPath);
    Directory.CreateDirectory(uploadsPath);

    var storedFileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
    var fullPath = Path.Combine(uploadsPath, storedFileName);

    await using (var stream = File.Create(fullPath))
    {
        await file.CopyToAsync(stream, cancellationToken);
    }

    var import = new VocabularyImport
    {
        FileName = storedFileName,
        OriginalFileName = file.FileName,
        Status = ImportStatus.Pending,
    };

    dbContext.VocabularyImports.Add(import);
    await dbContext.SaveChangesAsync(cancellationToken);

    var pairs = importService.Parse(fullPath);
    return Results.Ok(new ImportPreviewResponse(import.Id, import.OriginalFileName, pairs, pairs.Count == 0 ? "No vocabulary pairs detected." : null));
}).DisableAntiforgery();

app.MapPost("/api/imports/commit", async (ImportCommitRequest request, AppDbContext dbContext, TextNormalizationService normalizer, CancellationToken cancellationToken) =>
{
    var import = await dbContext.VocabularyImports.FirstOrDefaultAsync(x => x.Id == request.ImportId, cancellationToken);
    if (import is null)
    {
        return Results.NotFound(new { message = "Import not found." });
    }

    if (request.Pairs.Count == 0)
    {
        return Results.BadRequest(new { message = "No rows supplied." });
    }

    var categoryNames = request.Pairs
        .Select(x => x.Category?.Trim())
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (categoryNames.Count == 0)
    {
        return Results.BadRequest(new { message = "Each imported row needs a category." });
    }

    var existingCategories = await dbContext.Categories
        .Where(x => categoryNames.Contains(x.Name))
        .ToListAsync(cancellationToken);

    foreach (var missingName in categoryNames.Where(name => existingCategories.All(x => !string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))))
    {
        var category = new Category { Name = missingName! };
        dbContext.Categories.Add(category);
        existingCategories.Add(category);
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    var categoryLookup = existingCategories.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    var existingKeys = await dbContext.WordEntries
        .Include(x => x.Variants)
        .AsNoTracking()
        .Select(x => new
        {
            CategoryId = x.CategoryId,
            English = x.Variants.Where(v => v.Language == AnswerLanguage.English).OrderBy(v => v.SortOrder).Select(v => v.NormalizedText).FirstOrDefault(),
            Spanish = x.Variants.Where(v => v.Language == AnswerLanguage.Spanish).OrderBy(v => v.SortOrder).Select(v => v.NormalizedText).FirstOrDefault(),
        })
        .ToListAsync(cancellationToken);

    var newWords = new List<WordEntry>();
    foreach (var pair in request.Pairs)
    {
        if (!categoryLookup.TryGetValue(pair.Category, out var category))
        {
            continue;
        }

        var englishAnswers = CleanAnswers(new[] { pair.English }.Concat(pair.AdditionalEnglishAnswers));
        var spanishAnswers = CleanAnswers(new[] { pair.Spanish }.Concat(pair.AdditionalSpanishAnswers));
        if (englishAnswers.Count == 0 || spanishAnswers.Count == 0)
        {
            continue;
        }

        var normalizedEnglish = normalizer.Normalize(englishAnswers[0]);
        var normalizedSpanish = normalizer.Normalize(spanishAnswers[0]);
        if (existingKeys.Any(x => x.CategoryId == category.Id && x.English == normalizedEnglish && x.Spanish == normalizedSpanish))
        {
            continue;
        }

        newWords.Add(new WordEntry
        {
            VocabularyImportId = import.Id,
            CategoryId = category.Id,
            Pronunciation = NullIfWhitespace(pair.Pronunciation),
            Comment = NullIfWhitespace(pair.Comment),
            AllowReverse = true,
            Gender = (GenderType)pair.Gender,
            Number = (NumberType)pair.Number,
            State = (StateType)pair.State,
            Variants = BuildVariants(englishAnswers, spanishAnswers, normalizer),
            Examples = BuildExamples(pair.Examples ?? []),
        });
    }

    dbContext.WordEntries.AddRange(newWords);
    import.ImportedCount = newWords.Count;
    import.Status = ImportStatus.Completed;
    import.Notes = $"Imported {newWords.Count} rows.";
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { imported = newWords.Count, totalSubmitted = request.Pairs.Count });
});

app.MapGet("/api/quiz/next", async (HttpRequest request, QuestionSelectorService selector, CancellationToken cancellationToken) =>
{
    var rawCategoryIds = request.Query["categoryIds"].ToArray();
    Guid[]? parsedCategoryIds = null;
    if (rawCategoryIds.Length > 0)
    {
        parsedCategoryIds = rawCategoryIds
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToArray();
    }

    var question = await selector.GetNextQuestionAsync(parsedCategoryIds, cancellationToken);
    return question is null
        ? Results.NotFound(new { message = "No words available yet. Add some cards first." })
        : Results.Ok(question);
});

app.MapPost("/api/quiz/submit", async (HttpRequest httpRequest, AppDbContext dbContext, TextNormalizationService normalizer, CloseMatchService closeMatchService, ProgressService progressService, IOptions<AppOptions> appOptions, CancellationToken cancellationToken) =>
{
    var request = await httpRequest.ReadFromJsonAsync<SubmitAttemptRequest>(cancellationToken);
    if (request is null || request.WordId == Guid.Empty)
    {
        return Results.BadRequest(new { message = "Invalid quiz submission." });
    }

    var word = await dbContext.WordEntries
        .Include(x => x.Progress)
        .Include(x => x.Variants)
        .Include(x => x.Examples)
        .FirstOrDefaultAsync(x => x.Id == request.WordId, cancellationToken);

    if (word is null)
    {
        return Results.NotFound(new { message = "Word not found." });
    }

    var expectedLanguage = request.PromptLanguage == PromptLanguage.English ? AnswerLanguage.Spanish : AnswerLanguage.English;
    var expectedAnswers = word.Variants.Where(x => x.Language == expectedLanguage).OrderBy(x => x.SortOrder).ToList();
    var expectedPrimary = expectedAnswers.First().Text;
    var promptText = word.Variants.Where(x => x.Language == (request.PromptLanguage == PromptLanguage.English ? AnswerLanguage.English : AnswerLanguage.Spanish)).OrderBy(x => x.SortOrder).Select(x => x.Text).First();
    var normalizedSubmitted = normalizer.Normalize(request.SubmittedAnswer, removeAccents: !appOptions.Value.StrictAccents);
    var strictNormalizedSubmitted = normalizer.Normalize(request.SubmittedAnswer, removeAccents: false);
    var acceptedAnswers = expectedAnswers.Select(x => x.Text).ToList();
    var normalizedExpected = expectedAnswers.Select(x => normalizer.Normalize(x.Text, removeAccents: !appOptions.Value.StrictAccents)).Distinct(StringComparer.Ordinal).ToList();
    var strictNormalizedExpected = expectedAnswers.Select(x => normalizer.Normalize(x.Text, removeAccents: false)).Distinct(StringComparer.Ordinal).ToList();

    var attributesCorrect = request.PromptLanguage == PromptLanguage.English
        || (request.Gender == (int)word.Gender && request.Number == (int)word.Number && request.State == (int)word.State);

    var accentMismatchOnly = normalizedExpected.Contains(normalizedSubmitted, StringComparer.Ordinal)
        && !strictNormalizedExpected.Contains(strictNormalizedSubmitted, StringComparer.Ordinal);

    if (!accentMismatchOnly && normalizedExpected.Contains(normalizedSubmitted, StringComparer.Ordinal) && attributesCorrect)
    {
        var streak = await progressService.RecordAttemptAsync(word, request.PromptLanguage, promptText, expectedPrimary, request.SubmittedAnswer, normalizedSubmitted, AttemptResultType.Exact, false, 0, cancellationToken);
        return Results.Ok(new SubmitAttemptResponse(true, false, false, expectedPrimary, acceptedAnswers, request.SubmittedAnswer, 0, AttemptResultType.Exact, "Correct.", await progressService.GetSummaryAsync(cancellationToken), streak, progressService.GetEncouragement(streak), (int)word.Gender, (int)word.Number, (int)word.State, word.Comment, word.Pronunciation, MapExamples(word)));
    }

    var closeCandidate = accentMismatchOnly
        ? closeMatchService.Evaluate(normalizedSubmitted, normalizedSubmitted)
        : normalizedExpected
            .Select(variant => closeMatchService.Evaluate(normalizedSubmitted, variant))
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

    if ((accentMismatchOnly || closeCandidate.IsClose) && request.AcceptCloseMatch is null && attributesCorrect)
    {
        var closeMessage = accentMismatchOnly
            ? "That looks close, but the accent mark is wrong. Did you know it but miss the accent?"
            : "That looks close. Did you know it but spell it wrong?";
        return Results.Ok(new SubmitAttemptResponse(false, true, true, expectedPrimary, acceptedAnswers, request.SubmittedAnswer, closeCandidate.Distance, null, closeMessage, await progressService.GetSummaryAsync(cancellationToken), word.Progress?.CurrentStreak ?? 0, progressService.GetEncouragement(word.Progress?.CurrentStreak ?? 0), (int)word.Gender, (int)word.Number, (int)word.State, word.Comment, word.Pronunciation, MapExamples(word)));
    }

    var resultType = (accentMismatchOnly || closeCandidate.IsClose) && request.AcceptCloseMatch == true && attributesCorrect
        ? AttemptResultType.TypoSaved
        : AttemptResultType.Wrong;

    var updatedStreak = await progressService.RecordAttemptAsync(word, request.PromptLanguage, promptText, expectedPrimary, request.SubmittedAnswer, normalizedSubmitted, resultType, accentMismatchOnly || closeCandidate.IsClose, closeCandidate.Distance, cancellationToken);
    var message = !attributesCorrect
        ? "Not quite — the grammar/context selections were wrong."
        : resultType == AttemptResultType.TypoSaved
            ? "Counted as known, but marked as a spelling miss."
            : "Not quite — this one will come back more often.";

    return Results.Ok(new SubmitAttemptResponse(resultType != AttemptResultType.Wrong, closeCandidate.IsClose, false, expectedPrimary, acceptedAnswers, request.SubmittedAnswer, closeCandidate.Distance, resultType, message, await progressService.GetSummaryAsync(cancellationToken), updatedStreak, progressService.GetEncouragement(updatedStreak), (int)word.Gender, (int)word.Number, (int)word.State, word.Comment, word.Pronunciation, MapExamples(word)));
});

app.MapGet("/api/progress/summary", async (ProgressService progressService, CancellationToken cancellationToken) =>
    Results.Ok(await progressService.GetSummaryAsync(cancellationToken)));

app.MapGet("/api/words/weak", async (ProgressService progressService, CancellationToken cancellationToken) =>
    Results.Ok((await progressService.GetSummaryAsync(cancellationToken)).WeakWords));

app.MapPost("/api/maintenance/reset-stats", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    dbContext.Attempts.RemoveRange(dbContext.Attempts);
    dbContext.WordProgressEntries.RemoveRange(dbContext.WordProgressEntries);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/maintenance/wipe-database", async (AppDbContext dbContext, CancellationToken cancellationToken) =>
{
    dbContext.Attempts.RemoveRange(dbContext.Attempts);
    dbContext.WordProgressEntries.RemoveRange(dbContext.WordProgressEntries);
    dbContext.WordVariants.RemoveRange(dbContext.WordVariants);
    dbContext.WordExamples.RemoveRange(dbContext.WordExamples);
    dbContext.WordEntries.RemoveRange(dbContext.WordEntries);
    dbContext.Categories.RemoveRange(dbContext.Categories);
    dbContext.VocabularyImports.RemoveRange(dbContext.VocabularyImports);
    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(new { ok = true });
});

app.MapFallbackToFile("index.html");
app.Run();

static WordListItemResponse MapWordResponse(WordEntry word) => new(
    word.Id,
    word.Variants.Where(x => x.Language == AnswerLanguage.English).OrderBy(x => x.SortOrder).Select(x => new AnswerVariantResponse(x.Id, x.Text, x.SortOrder)).ToList(),
    word.Variants.Where(x => x.Language == AnswerLanguage.Spanish).OrderBy(x => x.SortOrder).Select(x => new AnswerVariantResponse(x.Id, x.Text, x.SortOrder)).ToList(),
    MapExamples(word),
    word.Pronunciation,
    word.Comment,
    word.AllowReverse,
    (int)word.Gender,
    (int)word.Number,
    (int)word.State,
    word.CategoryId,
    word.Category.Name);

static IReadOnlyList<ExampleSentenceResponse> MapExamples(WordEntry word) => word.Examples.OrderBy(x => x.SortOrder).Select(x => new ExampleSentenceResponse(x.Id, x.SpanishText, x.EnglishText, x.SortOrder)).ToList();

static List<string> CleanAnswers(IEnumerable<string> values) => values.Select(x => x?.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

static List<WordExample> BuildExamples(IEnumerable<ExampleSentenceInput> examples) => examples
    .Select((example, index) => new
    {
        SpanishText = example.SpanishText?.Trim(),
        EnglishText = example.EnglishText?.Trim(),
        SortOrder = index,
    })
    .Where(x => !string.IsNullOrWhiteSpace(x.SpanishText) && !string.IsNullOrWhiteSpace(x.EnglishText))
    .Select(x => new WordExample
    {
        SpanishText = x.SpanishText!,
        EnglishText = x.EnglishText!,
        SortOrder = x.SortOrder,
    })
    .ToList();

static List<WordVariant> BuildVariants(IReadOnlyList<string> englishAnswers, IReadOnlyList<string> spanishAnswers, TextNormalizationService normalizer)
{
    var variants = new List<WordVariant>();
    variants.AddRange(englishAnswers.Select((text, index) => new WordVariant
    {
        Language = AnswerLanguage.English,
        Text = text,
        NormalizedText = normalizer.Normalize(text),
        SortOrder = index,
    }));
    variants.AddRange(spanishAnswers.Select((text, index) => new WordVariant
    {
        Language = AnswerLanguage.Spanish,
        Text = text,
        NormalizedText = normalizer.Normalize(text),
        SortOrder = index,
    }));
    return variants;
}

static async Task<Guid> EnsureManualImportAsync(AppDbContext dbContext, CancellationToken cancellationToken)
{
    var importId = await dbContext.VocabularyImports.Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
    if (importId is not null)
    {
        return importId.Value;
    }

    var import = new VocabularyImport
    {
        FileName = "manual-entry",
        OriginalFileName = "manual-entry",
        Status = ImportStatus.Completed,
        ImportedCount = 0,
        Notes = "Created manually in app"
    };

    dbContext.VocabularyImports.Add(import);
    await dbContext.SaveChangesAsync(cancellationToken);
    return import.Id;
}

static async Task<Category> GetCategoryAsync(Guid categoryId, AppDbContext dbContext, CancellationToken cancellationToken)
{
    var category = await dbContext.Categories.FirstOrDefaultAsync(x => x.Id == categoryId, cancellationToken);
    if (category is null)
    {
        throw new InvalidOperationException("Category not found.");
    }

    return category;
}

static string EscapeCsv(string? value) => (value ?? string.Empty).Replace("\"", "\"\"");
static string? NullIfWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

static async Task EnsureDatabaseSchemaAsync(AppDbContext dbContext, CancellationToken cancellationToken)
{
    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
}

