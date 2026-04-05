namespace SpanishPractice.Api.Contracts;

public record CategoryResponse(Guid Id, string Name);
public record CreateCategoryRequest(string Name);
public record AnswerVariantResponse(Guid Id, string Text, int SortOrder);
public record ExampleSentenceResponse(Guid Id, string SpanishText, string EnglishText, int SortOrder);

public record WordListItemResponse(
    Guid Id,
    IReadOnlyList<AnswerVariantResponse> EnglishAnswers,
    IReadOnlyList<AnswerVariantResponse> SpanishAnswers,
    IReadOnlyList<ExampleSentenceResponse> Examples,
    string? Pronunciation,
    string? Comment,
    bool AllowReverse,
    int Gender,
    int Number,
    int State,
    Guid CategoryId,
    string CategoryName);

public record ExampleSentenceInput(string SpanishText, string EnglishText);

public class SaveWordRequest
{
    public string EnglishPrompt { get; set; } = string.Empty;
    public List<string> AdditionalEnglishAnswers { get; set; } = new();
    public string SpanishPrompt { get; set; } = string.Empty;
    public List<string> AdditionalSpanishAnswers { get; set; } = new();
    public string? Pronunciation { get; set; }
    public string? Comment { get; set; }
    public bool AllowReverse { get; set; } = true;
    public List<ExampleSentenceInput> Examples { get; set; } = new();
    public int Gender { get; set; }
    public int Number { get; set; }
    public int State { get; set; }
    public string CategoryId { get; set; } = string.Empty;
}
