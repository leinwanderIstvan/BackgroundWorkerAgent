namespace BackgroundWorkerAgent.Core.Models;

/// <summary>
/// Represents a single response from an LLM provider.
/// </summary>
public record LlmResponse
{
    public required string ModelName { get; init; }
    public required string Provider { get; init; }
    public required string ResponseText { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int? TokenCount { get; init; }

    public static LlmResponse Create(
        string modelName,
        string provider,
        string responseText,
        int? tokenCount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(responseText);

        return new LlmResponse
        {
            ModelName = modelName,
            Provider = provider,
            ResponseText = responseText,
            TokenCount = tokenCount
        };
    }
}
