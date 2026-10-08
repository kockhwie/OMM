namespace OMM.Shared.DecisionSupport;

public sealed class DecisionPathwayService(IDecisionPathwayStore store) : IDecisionPathwayService
{
    private const int PageSize = 10;

    public async Task<Guid> SavePathwayAsync(DecisionPathwayRecord record, CancellationToken ct = default)
    {
        if (record.Id == Guid.Empty)
        {
            record.Id = Guid.NewGuid();
        }

        if (record.CreatedAt == default)
        {
            record.CreatedAt = DateTimeOffset.UtcNow;
        }

        await store.SaveAsync(record, ct);
        return record.Id;
    }

    public Task<DecisionPathwayPage> GetPathwaysAsync(string userId, int page, CancellationToken ct = default) =>
        store.GetPageAsync(userId, Math.Max(1, page), PageSize, ct);
}
