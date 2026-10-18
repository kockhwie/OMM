namespace OMM.Shared.DecisionSupport;

public interface IDecisionPathwayStore
{
    Task SaveAsync(DecisionPathwayRecord record, CancellationToken ct);

    Task<DecisionPathwayPage> GetPageAsync(string userId, int page, int pageSize, string? statusFilter = null, CancellationToken ct = default);

    Task<bool> UpdateStatusAsync(Guid id, string userId, string newStatus, CancellationToken ct = default);
}
