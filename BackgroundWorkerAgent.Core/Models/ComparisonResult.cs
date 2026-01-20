using System.Text.RegularExpressions;

namespace BackgroundWorkerAgent.Core.Models;

public record ComparisonResult(
    string Prompt,
    string GptResponse,
    string ClaudeResponse,
    IReadOnlyList<string> GptOnlyWords,
    IReadOnlyList<string> ClaudeOnlyWords,
    IReadOnlyList<string> SharedWords,
    DateTime ComparedAt)
{
    public static ComparisonResult Create(string prompt, string gptResponse, string claudeResponse)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(gptResponse);
        ArgumentNullException.ThrowIfNull(claudeResponse);

        var gptWords = ExtractWords(gptResponse);
        var claudeWords = ExtractWords(claudeResponse);

        var gptOnly = gptWords.Except(claudeWords).ToList();
        var claudeOnly = claudeWords.Except(gptWords).ToList();
        var shared = gptWords.Intersect(claudeWords).ToList();

        return new ComparisonResult(
            prompt,
            gptResponse,
            claudeResponse,
            gptOnly,
            claudeOnly,
            shared,
            DateTime.UtcNow);
    }

    private static HashSet<string> ExtractWords(string text)
    {
        // Extract alphanumeric words, lowercase for case-insensitive comparison
        var matches = Regex.Matches(text, @"\b[a-zA-Z]{3,}\b");
        return matches
            .Select(m => m.Value.ToLowerInvariant())
            .ToHashSet();
    }
}