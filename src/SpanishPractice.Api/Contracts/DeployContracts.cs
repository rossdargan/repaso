namespace SpanishPractice.Api.Contracts;

public record DeployWebhookRequest(string? Ref, string? Repository, string? Sha);
