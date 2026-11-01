using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Shared.Infrastructure.Settings;
using OMM.Shared.Inquiry;
using OMM.Shared.Models.Phase5;

namespace OMM.Public.Services;

public sealed class InquiryCaptureService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ISystemSettingsService systemSettings,
    IInquiryMessageProtector messageProtector,
    TimeProvider timeProvider,
    ILogger<InquiryCaptureService> logger) : IInquiryCaptureService
{
    private const int DefaultRetentionYears = 3;
    private const int MaximumRawMessageLength = 20_000;

    public async Task<long> StartSessionAsync(InquirySessionStartRequest request, CancellationToken cancellationToken = default)
    {
        EnsureMessageProtectionConfigured();
        ArgumentNullException.ThrowIfNull(request);
        ValidateBounded(request.EntryPoint, nameof(request.EntryPoint), 50);
        ValidateBounded(request.RequestedMode, nameof(request.RequestedMode), 40);
        ValidateBounded(request.FlowVersion, nameof(request.FlowVersion), 40);

        if ((request.IsAnonymous && !string.IsNullOrWhiteSpace(request.UserId)) ||
            (!request.IsAnonymous && string.IsNullOrWhiteSpace(request.UserId)))
        {
            throw new ArgumentException("Anonymous inquiry sessions must not include a user ID, and member sessions must include one.", nameof(request));
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var session = new InquirySession
        {
            UserId = request.UserId,
            EntryPoint = request.EntryPoint.Trim(),
            RequestedMode = request.RequestedMode.Trim(),
            IsAnonymous = request.IsAnonymous,
            FlowVersion = request.FlowVersion.Trim(),
            StartedAt = now,
            ExpiresAt = now.AddYears(await GetRetentionYearsAsync(cancellationToken))
        };

        db.InquirySessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created inquiry session {InquirySessionId} with configured retention.", session.Id);
        return session.Id;
    }

    public async Task<long> PersistTurnAsync(
        long sessionId,
        string? userId,
        int turnNumber,
        InquiryTurnCapture turn,
        string rawMemberMessage,
        CancellationToken cancellationToken = default)
    {
        EnsureMessageProtectionConfigured();
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawMemberMessage);
        if (rawMemberMessage.Length > MaximumRawMessageLength)
        {
            throw new ArgumentOutOfRangeException(nameof(rawMemberMessage), $"Inquiry messages cannot exceed {MaximumRawMessageLength} characters.");
        }
        if (turnNumber < 1 || turn.AiCallAttemptCount is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(turnNumber), "Turn number must be positive and per-turn AI attempts cannot exceed six.");
        }
        if (turn.Confidence is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(nameof(turn), "Confidence must be between zero and one.");
        }
        ValidateBounded(turn.DetectedIntent, nameof(turn.DetectedIntent), 80, allowNull: true);
        ValidateBounded(turn.EventType, nameof(turn.EventType), 80, allowNull: true);
        ValidateBounded(turn.VerificationStatus, nameof(turn.VerificationStatus), 40, allowNull: true);
        ValidateBounded(turn.RequestedMode, nameof(turn.RequestedMode), 40);
        ValidateBounded(turn.EffectiveMode, nameof(turn.EffectiveMode), 40);
        ValidateBounded(turn.FallbackReason, nameof(turn.FallbackReason), 300, allowNull: true);

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.InquirySessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Inquiry session {sessionId} was not found.");
        if (!string.Equals(session.UserId, userId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The inquiry session does not belong to the current member.");
        }
        if (session.ExpiresAt <= timeProvider.GetUtcNow())
        {
            throw new InvalidOperationException("The inquiry session has expired.");
        }

        var now = timeProvider.GetUtcNow();
        var protectedMessage = messageProtector.Protect(rawMemberMessage);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var inquiryTurn = new InquiryTurn
        {
            InquirySessionId = session.Id,
            TurnNumber = turnNumber,
            PromptHash = ComputePrivacySafeHash(protectedMessage),
            DetectedIntent = turn.DetectedIntent?.Trim(),
            EventType = turn.EventType?.Trim(),
            VerificationStatus = turn.VerificationStatus?.Trim(),
            Confidence = turn.Confidence,
            PromptVersionId = turn.PromptVersionId,
            UsedEmergencyPrompt = turn.UsedEmergencyPrompt,
            RequestedMode = turn.RequestedMode.Trim(),
            EffectiveMode = turn.EffectiveMode.Trim(),
            FallbackReason = turn.FallbackReason?.Trim(),
            AiCallAttemptCount = turn.AiCallAttemptCount,
            CreatedAt = now
        };

        db.InquiryTurns.Add(inquiryTurn);
        await db.SaveChangesAsync(cancellationToken);
        db.InquiryRawMessages.Add(new InquiryRawMessage
        {
            InquiryTurnId = inquiryTurn.Id,
            MessageText = protectedMessage,
            CreatedAt = now,
            ExpiresAt = now.AddYears(await GetRetentionYearsAsync(cancellationToken))
        });
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        logger.LogInformation("Persisted inquiry session {InquirySessionId}, turn {TurnNumber}.", session.Id, turnNumber);
        return inquiryTurn.Id;
    }

    public async Task CompleteSessionAsync(long sessionId, string? userId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var session = await db.InquirySessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Inquiry session {sessionId} was not found.");
        if (!string.Equals(session.UserId, userId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("The inquiry session does not belong to the current member.");
        }

        session.CompletedAt ??= timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetRetentionYearsAsync(CancellationToken cancellationToken)
    {
        var value = await systemSettings.GetSettingAsync(SystemSettingKeys.InquiryRetentionYears, cancellationToken);
        return int.TryParse(value, out var years) && years is >= 1 and <= 10
            ? years
            : DefaultRetentionYears;
    }

    private void EnsureMessageProtectionConfigured()
    {
        if (!messageProtector.IsConfigured)
        {
            throw new InvalidOperationException("Inquiry message capture is unavailable until shared message encryption is configured.");
        }
    }

    private static string ComputePrivacySafeHash(string message)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidateBounded(string? value, string parameterName, int maxLength, bool allowNull = false)
    {
        if ((!allowNull && string.IsNullOrWhiteSpace(value)) || (value?.Length ?? 0) > maxLength)
        {
            throw new ArgumentException($"Value is required and must not exceed {maxLength} characters.", parameterName);
        }
    }
}
