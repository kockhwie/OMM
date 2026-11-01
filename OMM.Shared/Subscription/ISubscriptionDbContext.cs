using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OMM.Shared.Models;
using OMM.Shared.Models.Phase5;

namespace OMM.Shared.Subscription;

public interface ISubscriptionDbContext : IAsyncDisposable, IDisposable
{
    DbSet<MemberSubscription> MemberSubscriptions { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<AiPromptMode> AiPromptModes { get; }
    DbSet<AiPromptVersion> AiPromptVersions { get; }
    DbSet<AiPromptAuditEvent> AiPromptAuditEvents { get; }
    DbSet<SubscriptionUpgradeRequest> SubscriptionUpgradeRequests { get; }
    DbSet<SubscriptionChangeEvent> SubscriptionChangeEvents { get; }
    DbSet<SubscriptionDowngradeEvent> SubscriptionDowngradeEvents { get; }
    DbSet<SubscriptionLifecycleJobRun> SubscriptionLifecycleJobRuns { get; }
    DbSet<SystemSetting> SystemSettings { get; }
    DbSet<InquirySession> InquirySessions { get; }
    DbSet<InquiryTelemetryEvent> InquiryTelemetryEvents { get; }
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ISubscriptionDbContextFactory
{
    Task<ISubscriptionDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default);
}

