namespace SpanishPractice.Api.Contracts;

public record ParsedWordPair(
    string Category,
    string English,
    IReadOnlyList<string> AdditionalEnglishAnswers,
    string Spanish,
    IReadOnlyList<string> AdditionalSpanishAnswers,
    int Gender,
    int Number,
    int State,
    string? Pronunciation = null,
    string? Comment = null,
    IReadOnlyList<ExampleSentenceInput>? Examples = null);

public record ImportPreviewResponse(Guid ImportId, string OriginalFileName, IReadOnlyList<ParsedWordPair> Pairs, string? Notes);

public record ImportCommitRequest(Guid ImportId, IReadOnlyList<ParsedWordPair> Pairs);
