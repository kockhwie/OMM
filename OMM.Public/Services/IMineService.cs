using OMM.Public.Models;

namespace OMM.Public.Services;

public interface IMineService
{
    Task<MinerProfile> GetMinerProfileAsync();
    Task<DashboardSummary> GetDashboardSummaryAsync();
    Task<List<Mine>> GetMinesAsync();
    Task<Mine?> GetMineByIdAsync(string id);
    Task AddMineAsync(Mine mine);
    Task DeleteMineAsync(string id);
    Task<List<Burden>> GetBurdensAsync();
    Task<Burden?> GetBurdenByIdAsync(string id);
    Task AddBurdenAsync(Burden burden);
    Task DeleteBurdenAsync(string id);
    Task<List<IncomeRecord>> GetIncomeRecordsAsync();
    Task AddIncomeRecordAsync(IncomeRecord record);
    Task DeleteIncomeRecordAsync(string id);
    Task<List<Goal>> GetGoalsAsync();
    Task AddGoalAsync(Goal goal);
    Task DeleteGoalAsync(string id);
    Task<List<Notification>> GetNotificationsAsync();
}
