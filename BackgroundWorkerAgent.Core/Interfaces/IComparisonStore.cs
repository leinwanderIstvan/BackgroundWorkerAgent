using BackgroundWorkerAgent.Core.Models;

namespace BackgroundWorkerAgent.Core.Interfaces;

public interface IComparisonStore
{
    Task SaveAsync(Comparison comparison, CancellationToken cancellationToken = default);
    Task<Comparison?> GetByIdAsync(Guid comparisonId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Comparison>> GetAllAsync(CancellationToken cancellationToken = default);
}