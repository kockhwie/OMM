namespace OMM.Shared.DecisionSupport;

public interface IDecisionPathwayStore
{
    Task SaveAsync(DecisionPathwayRecord record, CancellationToken ct);

    Task<DecisionPathwayPage> GetPageAsync(string userId, int page, int pageSize, CancellationToken ct);
}
