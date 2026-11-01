using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Inquiry;
using OMM.Shared.Models.Phase5;

namespace OMM.Public.Services;

public sealed class InquiryAnalyticsService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ISystemSettingsService systemSettings,
    TimeProvider timeProvider) : IInquiryAnalyticsService
{
    private const int MaximumPayloadLength = 4_000;

    public async Task RecordEntityLinksAsync(IReadOnlyCollection<InquiryEntityLinkCapture> links, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(links);
        if (links.Count == 0) return;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        foreach (var link in links.Take(20))
        {
            ValidateRequired(link.EntityType, 40, nameof(link.EntityType));
            ValidateRequired(link.EntityKey, 160, nameof(link.EntityKey));
            if (link.Confidence is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(link.Confidence));
            db.InquiryEntityLinks.Add(new InquiryEntityLink
            {
                InquirySessionId = link.SessionId,
                InquiryTurnId = link.TurnId,
                EntityType = link.EntityType.Trim(),
                EntityKey = link.EntityKey.Trim(),
                DisplayName = Trim(link.DisplayName, 200),
                ResolutionMethod = TrimRequired(link.ResolutionMethod, 40),
                Confidence = link.Confidence,
                IsResolved = link.IsResolved,
                UserConfirmed = link.UserConfirmed,
                CreatedAt = timeProvider.GetUtcNow()
            });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordSelectionAsync(InquirySelectionCapture selection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.InquirySelectionEvents.Add(new InquirySelectionEvent
        {
            InquirySessionId = selection.SessionId,
            InquiryTurnId = selection.TurnId,
            EventType = TrimRequired(selection.EventType, 60),
            PurposeCode = Trim(selection.PurposeCode, 50),
            SelectionKey = Trim(selection.SelectionKey, 120),
            MetadataJson = ValidateRedactedJson(selection.RedactedMetadataJson),
            OccurredAt = timeProvider.GetUtcNow()
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordOutcomeAsync(InquiryOutcomeCapture outcome, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.ClarificationCount < 0 || outcome.ClarificationCount > 20) throw new ArgumentOutOfRangeException(nameof(outcome.ClarificationCount));
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var record = await db.InquiryOutcomes.SingleOrDefaultAsync(item => item.InquirySessionId == outcome.SessionId, cancellationToken);
        if (record is null)
        {
            record = new InquiryOutcome { InquirySessionId = outcome.SessionId, CreatedAt = now };
            db.InquiryOutcomes.Add(record);
        }
        record.FinalRoute = Trim(outcome.FinalRoute, 80);
        record.ClarificationCount = outcome.ClarificationCount;
        record.AbandonmentPoint = Trim(outcome.AbandonmentPoint, 80);
        record.CompletionStatus = TrimRequired(outcome.CompletionStatus, 30);
        record.AlertCreated = outcome.AlertCreated;
        record.SavedAnalysis = outcome.SavedAnalysis;
        record.PathwayCompleted = outcome.PathwayCompleted;
        record.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordTelemetryAsync(InquiryTelemetryCapture telemetry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var payload = ValidateRedactedJson(telemetry.RedactedPayloadJson);
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.InquiryTelemetryEvents.Add(new InquiryTelemetryEvent
        {
            InquirySessionId = telemetry.SessionId,
            UserId = Trim(telemetry.UserId, 450),
            EventType = TrimRequired(telemetry.EventType, 60),
            Stage = Trim(telemetry.Stage, 40),
            PayloadJson = payload,
            OccurredAt = timeProvider.GetUtcNow(),
            ExpiresAt = timeProvider.GetUtcNow().AddYears(await GetRetentionYearsAsync(cancellationToken))
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetRetentionYearsAsync(CancellationToken cancellationToken)
    {
        var value = await systemSettings.GetSettingAsync(SystemSettingKeys.InquiryRetentionYears, cancellationToken);
        return int.TryParse(value, out var years) && years is >= 1 and <= 10 ? years : 3;
    }

    private static string ValidateRedactedJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "{}";
        if (value.Length > MaximumPayloadLength) throw new ArgumentOutOfRangeException(nameof(value));
        using var document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Telemetry payload must be a JSON object.", nameof(value));
        return value;
    }

    private static string TrimRequired(string value, int maxLength) => Trim(value, maxLength) ?? throw new ArgumentException("A bounded value is required.");
    private static string? Trim(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, maxLength)];
    private static void ValidateRequired(string value, int maxLength, string name) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength) throw new ArgumentException("Value is required and bounded.", name); }
}
