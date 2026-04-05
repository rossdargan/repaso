namespace SpanishPractice.Api.Models;

public enum PromptLanguage
{
    English = 1,
    Spanish = 2
}

public enum AttemptResultType
{
    Exact = 1,
    TypoSaved = 2,
    Wrong = 3
}

public enum ImportStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3
}

public enum GenderType
{
    NotApplicable = 0,
    Masculine = 1,
    Feminine = 2
}

public enum NumberType
{
    NotApplicable = 0,
    Singular = 1,
    Plural = 2
}

public enum StateType
{
    NotApplicable = 0,
    Permanent = 1,
    Temporary = 2
}

public enum AnswerLanguage
{
    English = 1,
    Spanish = 2
}
