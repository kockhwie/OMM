namespace OMM.Shared.DecisionSupport;

public interface IDecisionPathwayService
{
    Task<Guid> SavePathwayAsync(DecisionPathwayRecord record, CancellationToken ct = default);

    Task<DecisionPathwayPage> GetPathwaysAsync(string userId, int page, CancellationToken ct = default);
}

public sealed record DecisionPathwayPage(
    IReadOnlyList<DecisionPathwayRecord> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
}
