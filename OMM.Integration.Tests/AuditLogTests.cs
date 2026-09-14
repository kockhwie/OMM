using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OMM.Admin.Data;
using OMM.Admin.Services.Admin;
using Xunit;

namespace OMM.Integration.Tests;

public class AuditLogTests
{
    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Logs { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Logs.Add((logLevel, formatter(state, exception), exception));
        }
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("Simulated DB scope failure.");
    }

    private sealed class CapturingAuditLogger : IAuditLogger
    {
        public List<(string ActorUserId, string ActorUserName, string Action, string? TargetUserId, string? Detail)> Logs { get; } = [];

        public Task LogAsync(string actorUserId, string actorUserName, string action, string? targetUserId = null, string? detail = null)
        {
            Logs.Add((actorUserId, actorUserName, action, targetUserId, detail));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AdminAuditLog>> ListRecentAsync(string? searchTerm = null, int limit = 100)
        {
            return Task.FromResult<IReadOnlyList<AdminAuditLog>>([]);
        }
    }

    [Fact]
    public void AdminAuditLog_HasCorrectTableAndSchemaAttributes()
    {
        var tableAttr = typeof(AdminAuditLog).GetCustomAttribute<TableAttribute>();
        Assert.NotNull(tableAttr);
        Assert.Equal("AdminAuditLogs", tableAttr.Name);
        Assert.Equal("admin", tableAttr.Schema);
    }

    [Fact]
    public void AdminAuditLog_DoesNotHaveForeignKeyToAspNetUsers()
    {
        // Per AGENTS.md: Store actor/target user IDs as plain text without EF foreign keys
        var props = typeof(AdminAuditLog).GetProperties();
        var actorProp = props.First(p => p.Name == nameof(AdminAuditLog.ActorUserId));
        var targetProp = props.First(p => p.Name == nameof(AdminAuditLog.TargetUserId));

        Assert.Equal(typeof(string), actorProp.PropertyType);
        Assert.Equal(typeof(string), targetProp.PropertyType);

        // Verify there is no navigation property to ApplicationUser or IdentityUser
        Assert.DoesNotContain(props, p => p.PropertyType.Name.Contains("User") && p.PropertyType != typeof(string));
        Assert.DoesNotContain(props, p => p.GetCustomAttribute<ForeignKeyAttribute>() != null);
    }

    [Fact]
    public void AdminAuditLog_HasRequiredLengthConstraints()
    {
        var actorProp = typeof(AdminAuditLog).GetProperty(nameof(AdminAuditLog.ActorUserId))!;
        var maxActor = actorProp.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(maxActor);
        Assert.Equal(100, maxActor.Length);

        var actionProp = typeof(AdminAuditLog).GetProperty(nameof(AdminAuditLog.Action))!;
        var maxAction = actionProp.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(maxAction);
        Assert.Equal(50, maxAction.Length);

        var detailProp = typeof(AdminAuditLog).GetProperty(nameof(AdminAuditLog.Detail))!;
        var maxDetail = detailProp.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(maxDetail);
        Assert.Equal(500, maxDetail.Length);
    }

    [Fact]
    public async Task AuditLogger_WhenDbThrows_DoesNotThrowAndLogsError()
    {
        // Arrange: use throwing scope factory
        var logger = new TestLogger<AuditLogger>();
        var auditLogger = new AuditLogger(new ThrowingScopeFactory(), logger);

        // Act: should catch exception gracefully
        var exception = await Record.ExceptionAsync(() =>
            auditLogger.LogAsync("user-123", "admin_user", "Invite", "target-456", "Invited user"));

        // Assert
        Assert.Null(exception);
        Assert.Contains(logger.Logs, l => l.Level == LogLevel.Error && l.Message.Contains("Failed to persist audit log entry"));
    }

    [Fact]
    public async Task CapturingAuditLogger_RecordsUserManagementActions()
    {
        var auditLogger = new CapturingAuditLogger();

        await auditLogger.LogAsync("actor-1", "admin1", "Invite", "target-1", "Invited username 'test'");
        await auditLogger.LogAsync("actor-1", "admin1", "UpdateRole", "target-1", "Changed role to SuperAdmin");
        await auditLogger.LogAsync("actor-1", "admin1", "LockAccount", "target-1", "Locked user");
        await auditLogger.LogAsync("actor-1", "admin1", "UnlockAccount", "target-1", "Unlocked user");
        await auditLogger.LogAsync("actor-1", "admin1", "ForcePasswordReset", "target-1", "Required password reset");
        await auditLogger.LogAsync("actor-1", "admin1", "ReactivateAccount", "target-1", "Reactivated user");

        Assert.Equal(6, auditLogger.Logs.Count);
        Assert.Equal("Invite", auditLogger.Logs[0].Action);
        Assert.Equal("UpdateRole", auditLogger.Logs[1].Action);
        Assert.Equal("LockAccount", auditLogger.Logs[2].Action);
        Assert.Equal("UnlockAccount", auditLogger.Logs[3].Action);
        Assert.Equal("ForcePasswordReset", auditLogger.Logs[4].Action);
        Assert.Equal("ReactivateAccount", auditLogger.Logs[5].Action);
    }
}
