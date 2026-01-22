using System.Text.RegularExpressions;

namespace BackgroundWorkerAgent.Core.Models;

/// <summary>
/// Analyzes word usage across multiple LLM responses.
/// Supports N responses - not locked to just GPT vs Claude.
/// </summary>
public record WordAnalysis
{
    public required IReadOnlyList<string> SharedWords { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> UniqueWordsByModel { get; init; }
    public DateTime AnalyzedAt { get; init; } = DateTime.UtcNow;
}


public static class WordAnalysisExtensions
{
    extension(WordAnalysis)
    {
        public static WordAnalysis Create(IReadOnlyList<LlmResponse> responses)
        {
            ArgumentNullException.ThrowIfNull(responses);

            if (responses.Count < 2)
            {
                throw new ArgumentException("Need at least 2 responses to compare.", nameof(responses));
            }

            // Extract words from each response, keyed by model name
            var wordsByModel = responses.ToDictionary(
                r => r.ModelName,
                r => ExtractWords(r.ResponseText));

            // Shared words = intersection of ALL responses
            var sharedWords = wordsByModel.Values
                .Aggregate((current, next) => current.Intersect(next).ToHashSet())
                .OrderBy(w => w)
                .ToList();

            // Unique words per model = words in this model but not in ANY other
            var allOtherWords = new Dictionary<string, HashSet<string>>();
            foreach (var model in wordsByModel.Keys)
            {
                allOtherWords[model] = wordsByModel
                    .Where(kvp => kvp.Key != model)
                    .SelectMany(kvp => kvp.Value)
                    .ToHashSet();
            }

            var uniqueWordsByModel = wordsByModel.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<string>)kvp.Value
                    .Except(allOtherWords[kvp.Key])
                    .OrderBy(w => w)
                    .ToList());

            return new WordAnalysis
            {
                SharedWords = sharedWords,
                UniqueWordsByModel = uniqueWordsByModel
            };
        }
    }

    private static HashSet<string> ExtractWords(string text)
    {
        var matches = Regex.Matches(text, @"\b[a-zA-Z]{3,}\b");
        return matches
            .Select(m => m.Value.ToLowerInvariant())
            .ToHashSet();
    }
}
