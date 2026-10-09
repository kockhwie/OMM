namespace OMM.Shared.DecisionSupport;

public interface IDecisionSignalCatalog
{
    Task<IReadOnlyList<SignalTemplate>> GetActiveSignalsAsync(CancellationToken cancellationToken = default);
}