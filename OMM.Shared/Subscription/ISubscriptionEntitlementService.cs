using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OMM.Shared.Infrastructure.Settings;

namespace OMM.Shared.Subscription;

public interface ISubscriptionEntitlementService
{
    Task<MemberEntitlement> GetEntitlementAsync(string? userId, CancellationToken cancellationToken = default);
    Task<RuntimeModeEvaluation> EvaluateRuntimeModeAsync(string? userId, string requestedMode, CancellationToken cancellationToken = default);
    void InvalidateEntitlementCache(string userId);
}

public sealed class SubscriptionEntitlementService(
    ISubscriptionDbContextFactory dbContextFactory,
    ISystemSettingsService systemSettingsService,
    IMemoryCache cache,
    TimeProvider timeProvider,
    ILogger<SubscriptionEntitlementService> logger) : ISubscriptionEntitlementService
{
    private static readonly TimeSpan FreshCacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxStaleWindow = TimeSpan.FromMinutes(5);
    private static string GetCacheKey(string userId) => $"entitlement:{userId}";

    private sealed record CachedEntitlement(MemberEntitlement Entitlement, DateTimeOffset CachedAt);

    public async Task<MemberEntitlement> GetEntitlementAsync(string? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return MemberEntitlement.NonMember;
        }

        var cacheKey = GetCacheKey(userId);
        var now = timeProvider.GetUtcNow();

        if (cache.TryGetValue<CachedEntitlement>(cacheKey, out var cached) && cached is not null)
        {
            if (now - cached.CachedAt < FreshCacheTtl)
            {
                return cached.Entitlement with { IsFromCache = true };
            }
        }

        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var userSubscriptions = await (
                from sub in db.MemberSubscriptions
                join plan in db.SubscriptionPlans on sub.SubscriptionPlanId equals plan.Id
                join mode in db.AiPromptModes on plan.PromptModeCode equals mode.Code
                where sub.UserId == userId &&
                      (sub.Status == SubscriptionConstants.Statuses.Active ||
                       sub.Status == SubscriptionConstants.Statuses.Scheduled ||
                       sub.Status == SubscriptionConstants.Statuses.Suspended)
                orderby sub.StartedAt
                select new
                {
                    Subscription = sub,
                    Plan = plan,
                    PromptMode = mode
                }
            ).ToListAsync(cancellationToken);

            var currentRecord = userSubscriptions.FirstOrDefault(x =>
                x.Subscription.EntitlementSlot == SubscriptionConstants.Slots.Current ||
                x.Subscription.Status == SubscriptionConstants.Statuses.Active);

            var scheduledRecord = userSubscriptions.FirstOrDefault(x =>
                x.Subscription.EntitlementSlot == SubscriptionConstants.Slots.Scheduled ||
                x.Subscription.Status == SubscriptionConstants.Statuses.Scheduled);

            var isCurrentPro = false;
            var effectiveRecord = currentRecord;

            if (currentRecord is not null)
            {
                var isCurrentExpired = currentRecord.Subscription.SubscriptionExpiryDate.HasValue &&
                                       currentRecord.Subscription.SubscriptionExpiryDate.Value <= now;

                if (isCurrentExpired)
                {
                    // If current subscription has expired, check if a scheduled renewal has started
                    if (scheduledRecord is not null &&
                        scheduledRecord.PromptMode.IsAdvisory &&
                        (scheduledRecord.Subscription.IsPaid || scheduledRecord.Subscription.ManuallyGranted) &&
                        scheduledRecord.Subscription.StartedAt <= now &&
                        scheduledRecord.Subscription.SubscriptionExpiryDate.HasValue &&
                        scheduledRecord.Subscription.SubscriptionExpiryDate.Value > now)
                    {
                        effectiveRecord = scheduledRecord;
                        isCurrentPro = true;
                    }
                    else
                    {
                        isCurrentPro = false;
                    }
                }
                else
                {
                    // Active Pro entitlement requires:
                    // 1. Status == Active (Suspended blocks access)
                    // 2. Advisory plan
                    // 3. Paid or ManuallyGranted
                    // 4. StartedAt <= UtcNow
                    // 5. Finite expiry date > UtcNow (null expiry on Pro is invalid)
                    if (currentRecord.Subscription.Status == SubscriptionConstants.Statuses.Active &&
                        currentRecord.PromptMode.IsAdvisory &&
                        (currentRecord.Subscription.IsPaid || currentRecord.Subscription.ManuallyGranted) &&
                        currentRecord.Subscription.StartedAt <= now &&
                        currentRecord.Subscription.SubscriptionExpiryDate.HasValue &&
                        currentRecord.Subscription.SubscriptionExpiryDate.Value > now)
                    {
                        isCurrentPro = true;
                    }
                }
            }

            var hasFutureScheduled = scheduledRecord is not null &&
                                     scheduledRecord.Subscription.StartedAt > now &&
                                     scheduledRecord.Subscription.Status == SubscriptionConstants.Statuses.Scheduled;

            MemberEntitlement entitlement;
            if (isCurrentPro && effectiveRecord is not null)
            {
                entitlement = new MemberEntitlement
                {
                    UserId = userId,
                    Tier = EntitlementTier.Pro,
                    IsAdvisoryPermitted = true,
                    ActiveSubscriptionId = effectiveRecord.Subscription.Id,
                    PlanId = effectiveRecord.Plan.Id,
                    PlanCode = effectiveRecord.Plan.Code,
                    PromptModeCode = effectiveRecord.PromptMode.Code,
                    IsPaid = effectiveRecord.Subscription.IsPaid,
                    ManuallyGranted = effectiveRecord.Subscription.ManuallyGranted,
                    StartedAt = effectiveRecord.Subscription.StartedAt,
                    SubscriptionExpiryDate = effectiveRecord.Subscription.SubscriptionExpiryDate,
                    HasScheduledRenewal = hasFutureScheduled,
                    ScheduledSubscriptionId = hasFutureScheduled ? scheduledRecord!.Subscription.Id : null,
                    ScheduledRenewalStartDate = hasFutureScheduled ? scheduledRecord!.Subscription.StartedAt : null,
                    DisplayLabel = SubscriptionConstants.DisplayLabels.Pro,
                    IsFromCache = false
                };
            }
            else
            {
                entitlement = new MemberEntitlement
                {
                    UserId = userId,
                    Tier = EntitlementTier.Standard,
                    IsAdvisoryPermitted = false,
                    ActiveSubscriptionId = effectiveRecord?.Subscription.Id,
                    PlanId = effectiveRecord?.Plan.Id,
                    PlanCode = effectiveRecord?.Plan.Code ?? SubscriptionConstants.PlanCodes.Standard,
                    PromptModeCode = SubscriptionConstants.PromptModes.Standard,
                    IsPaid = effectiveRecord?.Subscription.IsPaid ?? false,
                    ManuallyGranted = effectiveRecord?.Subscription.ManuallyGranted ?? false,
                    StartedAt = effectiveRecord?.Subscription.StartedAt,
                    SubscriptionExpiryDate = effectiveRecord?.Subscription.SubscriptionExpiryDate,
                    HasScheduledRenewal = hasFutureScheduled,
                    ScheduledSubscriptionId = hasFutureScheduled ? scheduledRecord!.Subscription.Id : null,
                    ScheduledRenewalStartDate = hasFutureScheduled ? scheduledRecord!.Subscription.StartedAt : null,
                    DisplayLabel = SubscriptionConstants.DisplayLabels.Standard,
                    IsFromCache = false
                };
            }

            cache.Set(cacheKey, new CachedEntitlement(entitlement, now), MaxStaleWindow);
            return entitlement;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve subscription entitlement from database for user {UserId}", userId);

            if (cache.TryGetValue<CachedEntitlement>(cacheKey, out var stale) && stale is not null)
            {
                if (now - stale.CachedAt <= MaxStaleWindow)
                {
                    return stale.Entitlement with { IsFromCache = true, IsStale = true };
                }
            }

            return MemberEntitlement.CreateUnavailable(userId);
        }
    }

    public async Task<RuntimeModeEvaluation> EvaluateRuntimeModeAsync(
        string? userId,
        string requestedMode,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequestedMode = (requestedMode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalizedRequestedMode))
        {
            normalizedRequestedMode = SubscriptionConstants.PromptModes.Standard;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new RuntimeModeEvaluation
            {
                RequestedMode = normalizedRequestedMode,
                EffectiveMode = SubscriptionConstants.PromptModes.Standard,
                DisplayLabel = string.Empty,
                IsAdvisoryPermitted = false,
                ProUnavailableFallback = false,
                FallbackReason = "Non-members are guided-wizard only.",
                Entitlement = MemberEntitlement.NonMember
            };
        }

        var entitlement = await GetEntitlementAsync(userId, cancellationToken);

        if (entitlement.Tier != EntitlementTier.Pro)
        {
            if (string.Equals(normalizedRequestedMode, SubscriptionConstants.PromptModes.Pro, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Members with Standard entitlement cannot request Pro mode.");
            }

            return new RuntimeModeEvaluation
            {
                RequestedMode = SubscriptionConstants.PromptModes.Standard,
                EffectiveMode = SubscriptionConstants.PromptModes.Standard,
                DisplayLabel = SubscriptionConstants.DisplayLabels.Standard,
                IsAdvisoryPermitted = false,
                ProUnavailableFallback = false,
                FallbackReason = null,
                Entitlement = entitlement
            };
        }

        // Entitled to Pro
        if (string.Equals(normalizedRequestedMode, SubscriptionConstants.PromptModes.Standard, StringComparison.OrdinalIgnoreCase))
        {
            // Voluntary educational selection
            return new RuntimeModeEvaluation
            {
                RequestedMode = SubscriptionConstants.PromptModes.Standard,
                EffectiveMode = SubscriptionConstants.PromptModes.Standard,
                DisplayLabel = SubscriptionConstants.DisplayLabels.Standard,
                IsAdvisoryPermitted = false,
                ProUnavailableFallback = false,
                FallbackReason = null,
                Entitlement = entitlement
            };
        }

        // Pro requested: verify PublicProEnabled setting
        var publicProEnabled = await systemSettingsService.IsPublicProEnabledAsync(cancellationToken);

        if (!publicProEnabled)
        {
            return new RuntimeModeEvaluation
            {
                RequestedMode = SubscriptionConstants.PromptModes.Pro,
                EffectiveMode = SubscriptionConstants.PromptModes.Standard,
                DisplayLabel = SubscriptionConstants.DisplayLabels.Standard,
                IsAdvisoryPermitted = false,
                ProUnavailableFallback = true,
                FallbackReason = SubscriptionConstants.DisplayLabels.ProUnavailableFallback,
                Entitlement = entitlement
            };
        }

        return new RuntimeModeEvaluation
        {
            RequestedMode = SubscriptionConstants.PromptModes.Pro,
            EffectiveMode = SubscriptionConstants.PromptModes.Pro,
            DisplayLabel = SubscriptionConstants.DisplayLabels.Pro,
            IsAdvisoryPermitted = true,
            ProUnavailableFallback = false,
            FallbackReason = null,
            Entitlement = entitlement
        };
    }

    public void InvalidateEntitlementCache(string userId)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            cache.Remove(GetCacheKey(userId));
        }
    }
}

