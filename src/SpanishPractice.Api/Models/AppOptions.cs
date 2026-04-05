namespace SpanishPractice.Api.Models;

public class AppOptions
{
    public const string SectionName = "App";

    public string UploadsPath { get; set; } = "wwwroot/uploads";
    public string? DeployWebhookSecret { get; set; }
    public bool StrictAccents { get; set; }
    public int DailyQuestionGoal { get; set; } = 20;
}
