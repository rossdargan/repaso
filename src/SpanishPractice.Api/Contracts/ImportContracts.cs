namespace SpanishPractice.Api.Contracts;

public record ParsedWordPair(string English, string Spanish, string? Pronunciation = null, string? Comment = null);

public record ImportPreviewResponse(Guid ImportId, string OriginalFileName, IReadOnlyList<ParsedWordPair> Pairs, string? Notes);

public record ImportCommitRequest(Guid ImportId, IReadOnlyList<ParsedWordPair> Pairs, Guid CategoryId);
