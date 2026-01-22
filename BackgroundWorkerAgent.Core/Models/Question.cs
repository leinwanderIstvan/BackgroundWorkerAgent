namespace BackgroundWorkerAgent.Core.Models;

/// <summary>
/// Represents the input that triggered a comparison - captures what was asked and where it came from.
/// </summary>
public record Question
{
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
    public required string Content { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public static Question FromFile(string filePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new Question
        {
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            Content = content
        };
    }
}
