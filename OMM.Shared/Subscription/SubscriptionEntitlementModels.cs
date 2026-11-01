namespace OMM.Shared.Subscription;

public enum EntitlementTier
{
    NonMember,
    Standard,
    Pro
}

public sealed record MemberEntitlement
{
    public string? UserId { get; init; }
    public EntitlementTier Tier { get; init; }
    public bool IsAdvisoryPermitted { get; init; }
    public long? ActiveSubscriptionId { get; init; }
    public int? PlanId { get; init; }
    public string? PlanCode { get; init; }
    public string? PromptModeCode { get; init; }
    public bool IsPaid { get; init; }
    public bool ManuallyGranted { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? SubscriptionExpiryDate { get; init; }
    public bool HasScheduledRenewal { get; init; }
    public long? ScheduledSubscriptionId { get; init; }
    public DateTimeOffset? ScheduledRenewalStartDate { get; init; }
    public string DisplayLabel { get; init; } = SubscriptionConstants.DisplayLabels.Standard;
    public bool IsFromCache { get; init; }
    public bool IsStale { get; init; }
    public bool IsUnavailable { get; init; }

    public static MemberEntitlement NonMember => new()
    {
        UserId = null,
        Tier = EntitlementTier.NonMember,
        IsAdvisoryPermitted = false,
        DisplayLabel = string.Empty
    };

    public static MemberEntitlement CreateUnavailable(string? userId) => new()
    {
        UserId = userId,
        Tier = EntitlementTier.Standard,
        IsAdvisoryPermitted = false,
        IsUnavailable = true,
        DisplayLabel = SubscriptionConstants.DisplayLabels.Standard
    };
}

public sealed record RuntimeModeEvaluation
{
    public string RequestedMode { get; init; } = SubscriptionConstants.PromptModes.Standard;
    public string EffectiveMode { get; init; } = SubscriptionConstants.PromptModes.Standard;
    public string DisplayLabel { get; init; } = SubscriptionConstants.DisplayLabels.Standard;
    public bool IsAdvisoryPermitted { get; init; }
    public bool ProUnavailableFallback { get; init; }
    public string? FallbackReason { get; init; }
    public MemberEntitlement Entitlement { get; init; } = MemberEntitlement.NonMember;
}

