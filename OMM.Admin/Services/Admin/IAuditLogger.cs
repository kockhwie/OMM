using System.Collections.Generic;
using System.Threading.Tasks;
using OMM.Admin.Data;

namespace OMM.Admin.Services.Admin;

public interface IAuditLogger
{
    Task LogAsync(string actorUserId, string actorUserName, string action, string? targetUserId = null, string? detail = null);
    Task LogInquiryDataActionAsync(string actorUserId, string actorUserName, string action, IReadOnlyCollection<long> sessionIds, IReadOnlyCollection<long> turnIds, int recordCount);
    Task<IReadOnlyList<AdminAuditLog>> ListRecentAsync(string? searchTerm = null, int limit = 100);
}
