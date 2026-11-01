using Microsoft.EntityFrameworkCore;
using OMM.Shared.Models.Phase5;
using OMM.Shared.Subscription;

namespace OMM.Public.Data;

public partial class ApplicationDbContext : ISubscriptionDbContext
{
    public DbSet<AiPromptMode> AiPromptModes => Set<AiPromptMode>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<MemberSubscription> MemberSubscriptions => Set<MemberSubscription>();
    public DbSet<SubscriptionUpgradeRequest> SubscriptionUpgradeRequests => Set<SubscriptionUpgradeRequest>();
    public DbSet<SubscriptionChangeEvent> SubscriptionChangeEvents => Set<SubscriptionChangeEvent>();
    public DbSet<SubscriptionDowngradeEvent> SubscriptionDowngradeEvents => Set<SubscriptionDowngradeEvent>();
    public DbSet<SubscriptionLifecycleJobRun> SubscriptionLifecycleJobRuns => Set<SubscriptionLifecycleJobRun>();
    public DbSet<AiPromptVersion> AiPromptVersions => Set<AiPromptVersion>();
    public DbSet<AiPromptAuditEvent> AiPromptAuditEvents => Set<AiPromptAuditEvent>();
    public DbSet<InquiryPurpose> InquiryPurposes => Set<InquiryPurpose>();
    public DbSet<InquirySession> InquirySessions => Set<InquirySession>();
    public DbSet<InquiryTurn> InquiryTurns => Set<InquiryTurn>();
    public DbSet<InquiryRawMessage> InquiryRawMessages => Set<InquiryRawMessage>();
    public DbSet<InquiryEntityLink> InquiryEntityLinks => Set<InquiryEntityLink>();
    public DbSet<InquirySelectionEvent> InquirySelectionEvents => Set<InquirySelectionEvent>();
    public DbSet<InquiryOutcome> InquiryOutcomes => Set<InquiryOutcome>();
    public DbSet<InquiryTelemetryEvent> InquiryTelemetryEvents => Set<InquiryTelemetryEvent>();
    public DbSet<Phase5EventTypeRule> Phase5EventTypeRules => Set<Phase5EventTypeRule>();
    public DbSet<Phase5ImpactRule> Phase5ImpactRules => Set<Phase5ImpactRule>();

    static partial void ConfigurePhase5Data(ModelBuilder modelBuilder)
    {
        Phase5ModelConfiguration.Configure(modelBuilder);

        modelBuilder.Entity<MemberSubscription>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SubscriptionUpgradeRequest>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SubscriptionChangeEvent>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<SubscriptionDowngradeEvent>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<InquirySession>().HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
