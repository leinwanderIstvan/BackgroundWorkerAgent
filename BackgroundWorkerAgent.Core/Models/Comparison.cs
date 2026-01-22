namespace BackgroundWorkerAgent.Core.Models;

/// <summary>
/// Root aggregate for a comparison event.
/// Contains the question, all LLM responses, and word analysis.
/// This is the entity that gets serialized to JSON for audit logging.
/// </summary>
public record Comparison
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Question Question { get; init; }
    public required IReadOnlyList<LlmResponse> Responses { get; init; }
    public required WordAnalysis Analysis { get; init; }
    public DateTime ComparedAt { get; init; } = DateTime.UtcNow;
}


public static class ComparisonExtensions
{
    extension(Comparison)
    {
        public static Comparison Create(Question question, IReadOnlyList<LlmResponse> responses)
        {
            ArgumentNullException.ThrowIfNull(question);
            ArgumentNullException.ThrowIfNull(responses);

            if (responses.Count < 2)
            {
                throw new ArgumentException("Need at least 2 responses to create a comparison.", nameof(responses));
            }

            var analysis = WordAnalysis.Create(responses);

            return new Comparison
            {
                Question = question,
                Responses = responses,
                Analysis = analysis
            };
        }
    }
}
