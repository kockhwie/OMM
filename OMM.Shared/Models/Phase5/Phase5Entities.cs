namespace OMM.Shared.Models.Phase5;

public sealed class AiPromptMode
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAdvisory { get; set; }
    public bool IsActive { get; set; } = true;
    public string SchemaVersion { get; set; } = string.Empty;
}

public sealed class SubscriptionPlan
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PromptModeCode { get; set; } = string.Empty;
    public int? DurationMonths { get; set; }
    public decimal? PriceAmount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? PaymentInstructions { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
}

public sealed class MemberSubscription
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public int SubscriptionPlanId { get; set; }
    public bool IsPaid { get; set; }
    public bool ManuallyGranted { get; set; }
    public string Status { get; set; } = "Active";
    public string? EntitlementSlot { get; set; } = "Current";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? SubscriptionExpiryDate { get; set; }
    public DateTimeOffset? TerminatedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? PaymentReference { get; set; }
}

public sealed class SubscriptionUpgradeRequest
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public int SubscriptionPlanId { get; set; }
    public decimal AmountDue { get; set; }
    public string CurrencyCode { get; set; } = "MYR";
    public string Status { get; set; } = "AwaitingPayment";
    public string? PaymentReference { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
    public long? ResultingSubscriptionId { get; set; }
}

public sealed class SubscriptionChangeEvent
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public long SubscriptionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string? PreviousStateJson { get; set; }
    public string? NewStateJson { get; set; }
}

public sealed class SubscriptionDowngradeEvent
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public long SourceSubscriptionId { get; set; }
    public int PreviousPlanId { get; set; }
    public int ResultingPlanId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset ExpiredAt { get; set; }
    public string NotificationStatus { get; set; } = "Pending";
    public DateTimeOffset? NotifiedAt { get; set; }
}

public sealed class SubscriptionLifecycleJobRun
{
    public long Id { get; set; }
    public string TriggerSource { get; set; } = "Scheduled";
    public string? TriggeredByAdminId { get; set; }
    public string Status { get; set; } = "Running";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int ScannedCount { get; set; }
    public int ActivatedCount { get; set; }
    public int ExpiredCount { get; set; }
    public int DowngradedCount { get; set; }
    public int NotifiedCount { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string? ErrorSummary { get; set; }
}

public sealed class AiPromptVersion
{
    public int Id { get; set; }
    public string PromptModeCode { get; set; } = string.Empty;
    public string VersionLabel { get; set; } = string.Empty;
    public string SystemInstruction { get; set; } = string.Empty;
    public string UserMessageTemplate { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
}

public sealed class AiPromptAuditEvent
{
    public long Id { get; set; }
    public int PromptVersionId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ActorType { get; set; } = string.Empty;
    public string? ActorId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Notes { get; set; }
    public string? ChangeMetadataJson { get; set; }
}

public sealed class InquiryPurpose
{
    public string Code { get; set; } = string.Empty;
    public string DisplayNameEn { get; set; } = string.Empty;
    public string? DisplayNameZh { get; set; }
    public string? DescriptionEn { get; set; }
    public string? DescriptionZh { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class InquirySession
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string EntryPoint { get; set; } = string.Empty;
    public string RequestedMode { get; set; } = "STANDARD";
    public bool IsAnonymous { get; set; }
    public string FlowVersion { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class InquiryTurn
{
    public long Id { get; set; }
    public long InquirySessionId { get; set; }
    public int TurnNumber { get; set; }
    public string? PromptHash { get; set; }
    public string? DetectedIntent { get; set; }
    public string? EventType { get; set; }
    public string? VerificationStatus { get; set; }
    public decimal? Confidence { get; set; }
    public int? PromptVersionId { get; set; }
    public bool UsedEmergencyPrompt { get; set; }
    public string RequestedMode { get; set; } = "STANDARD";
    public string EffectiveMode { get; set; } = "STANDARD";
    public string? FallbackReason { get; set; }
    public int AiCallAttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class InquiryRawMessage
{
    public long InquiryTurnId { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class InquiryEntityLink
{
    public long Id { get; set; }
    public long InquirySessionId { get; set; }
    public long? InquiryTurnId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityKey { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string ResolutionMethod { get; set; } = "Unresolved";
    public decimal? Confidence { get; set; }
    public bool IsResolved { get; set; }
    public bool UserConfirmed { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class InquirySelectionEvent
{
    public long Id { get; set; }
    public long InquirySessionId { get; set; }
    public long? InquiryTurnId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? PurposeCode { get; set; }
    public string? SelectionKey { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class InquiryOutcome
{
    public long Id { get; set; }
    public long InquirySessionId { get; set; }
    public string? FinalRoute { get; set; }
    public int ClarificationCount { get; set; }
    public string? AbandonmentPoint { get; set; }
    public string CompletionStatus { get; set; } = "InProgress";
    public bool AlertCreated { get; set; }
    public bool SavedAnalysis { get; set; }
    public bool PathwayCompleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class InquiryTelemetryEvent
{
    public long Id { get; set; }
    public long? InquirySessionId { get; set; }
    public string? UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Stage { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class Phase5EventTypeRule
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsSensitive { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Phase5ImpactRule
{
    public long Id { get; set; }
    public int EventTypeRuleId { get; set; }
    public string SourceEntityType { get; set; } = string.Empty;
    public string SourceEntityKey { get; set; } = string.Empty;
    public string TargetEntityType { get; set; } = string.Empty;
    public string TargetEntityKey { get; set; } = string.Empty;
    public string TransmissionMechanism { get; set; } = string.Empty;
    public string Direction { get; set; } = "Mixed";
    public string Strength { get; set; } = "Moderate";
    public string TimeHorizon { get; set; } = "MediumTerm";
    public string? Assumptions { get; set; }
    public bool MalaysiaRelevant { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
}
