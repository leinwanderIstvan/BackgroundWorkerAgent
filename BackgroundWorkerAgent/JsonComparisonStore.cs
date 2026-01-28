using System.Text.Json;
using BackgroundWorkerAgent.Core.Interfaces;
using BackgroundWorkerAgent.Core.Models;

namespace BackgroundWorkerAgent;

// File-based persistence using one JSON file per Comparison
internal sealed class JsonComparisonStore(string directoryPath) : IComparisonStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SaveAsync(Comparison comparison, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(comparison);

        Directory.CreateDirectory(directoryPath);

        var fileName = $"{comparison.ComparedAt:yyyyMMdd_HHmmss}_{comparison.Id}.json";
        var filePath = Path.Combine(directoryPath, fileName);

        // Stream lets SerializeAsync write directly to file without allocating the full JSON string in memory
        await using var stream = File.Create(filePath);
        // ConfigureAwait(false): still waits for result, but any thread can pick up after -
        // avoids overhead of returning to the original thread (safe in library/helper code)
        await JsonSerializer.SerializeAsync(stream, comparison, s_jsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Comparison?> GetByIdAsync(Guid comparisonId, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            return null;

        // FirstOrDefault: returns null if no file matches instead of throwing (not-found is expected, not exceptional)
        var matchingFile = Directory
            .EnumerateFiles(directoryPath, $"*_{comparisonId}.json")
            .FirstOrDefault();

        if (matchingFile is null)
            return null;

        await using var stream = File.OpenRead(matchingFile);
        return await JsonSerializer.DeserializeAsync<Comparison>(stream, s_jsonOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Comparison>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            return [];

        var files = Directory
            .EnumerateFiles(directoryPath, "*.json")
            .OrderBy(f => f);

        var comparisons = new List<Comparison>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await using var stream = File.OpenRead(file);
            var comparison = await JsonSerializer.DeserializeAsync<Comparison>(
                stream, s_jsonOptions, cancellationToken).ConfigureAwait(false);

            if (comparison is not null)
                comparisons.Add(comparison);
        }

        return comparisons;
    }
}
