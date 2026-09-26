using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Public.Validation;

namespace OMM.Public.Services;

public sealed class MemberRecordService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    AuthenticationStateProvider authenticationStateProvider,
    IMinerProfileService profileService)
{
    public static decimal MonthlyEquivalent(decimal amount, RecordFrequency frequency) => frequency switch
    {
        RecordFrequency.Monthly => amount,
        RecordFrequency.Annual => amount / 12m,
        RecordFrequency.OneOff => 0m,
        _ => 0m
    };

    public async Task<List<Burden>> GetBurdensAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await db.Burdens.AsNoTracking()
            .Include(item => item.Currency)
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return records.Select(MapBurden).ToList();
    }

    public async Task<Burden?> GetBurdenByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var recordId)) return null;
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var record = await db.Burdens.AsNoTracking().Include(item => item.Currency)
            .SingleOrDefaultAsync(item => item.Id == recordId && item.UserId == userId, cancellationToken);
        return record is null ? null : MapBurden(record);
    }

    public async Task AddBurdenAsync(Burden burden, CancellationToken cancellationToken = default)
    {
        PublicInputValidator.ValidateDisplayText("Burden name", burden.Name);
        if (burden.Balance < 0 || burden.OriginalAmount < 0 || burden.MonthlyPayment < 0)
            throw new ArgumentException("Burden amounts cannot be negative.", nameof(burden));

        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currencyId = await ResolveCurrencyIdAsync(db, burden.Currency, cancellationToken);
        var linkedMineId = await ResolveMineIdAsync(db, userId, burden.LinkedMineId, cancellationToken);
        db.Burdens.Add(new BurdenEntity
        {
            UserId = userId,
            Name = burden.Name.Trim(),
            Type = burden.Type,
            Balance = burden.Balance,
            OriginalAmount = burden.OriginalAmount > 0 ? burden.OriginalAmount : burden.Balance,
            InterestRate = burden.InterestRate,
            MonthlyPayment = burden.MonthlyPayment,
            CurrencyId = currencyId,
            LinkedMineId = linkedMineId,
            MaturityDate = ParseDate(burden.MaturityDate),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteBurdenAsync(string id, CancellationToken cancellationToken = default) =>
        await SoftDeleteAsync< BurdenEntity>(id, cancellationToken);

    public async Task<bool> UpdateBurdenAsync(Burden burden, CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        if (!Guid.TryParse(burden.Id, out var id)) return false;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Burdens.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (entity is null) return false;
        entity.Name = burden.Name.Trim(); entity.Type = burden.Type; entity.Balance = burden.Balance;
        entity.OriginalAmount = burden.OriginalAmount; entity.InterestRate = burden.InterestRate; entity.MonthlyPayment = burden.MonthlyPayment;
        entity.CurrencyId = await ResolveCurrencyIdAsync(db, burden.Currency, cancellationToken);
        entity.LinkedMineId = await ResolveMineIdAsync(db, userId, burden.LinkedMineId, cancellationToken);
        entity.MaturityDate = ParseDate(burden.MaturityDate); entity.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken); return true;
    }

    public async Task<List<IncomeRecord>> GetIncomeRecordsAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await db.IncomeRecords.AsNoTracking()
            .Include(item => item.Currency)
            .Include(item => item.Mine)
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.RecordDate)
            .ToListAsync(cancellationToken);
        return records.Select(MapIncome).ToList();
    }

    public async Task AddIncomeRecordAsync(IncomeRecord record, CancellationToken cancellationToken = default)
    {
        PublicInputValidator.ValidateDisplayText("Income source", record.Source);
        if (record.Amount <= 0) throw new ArgumentException("Income amount must be greater than zero.", nameof(record));

        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currencyId = await ResolveCurrencyIdAsync(db, record.Currency, cancellationToken);
        var mineId = await ResolveMineIdAsync(db, userId, record.MineId, cancellationToken);
        db.IncomeRecords.Add(new IncomeRecordEntity
        {
            UserId = userId,
            Source = record.Source.Trim(),
            Classification = record.Classification,
            Amount = record.Amount,
            CurrencyId = currencyId,
            Frequency = ParseFrequency(record.Frequency),
            MineId = mineId,
            RecordDate = ParseDate(record.Date) ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteIncomeRecordAsync(string id, CancellationToken cancellationToken = default) =>
        await SoftDeleteAsync<IncomeRecordEntity>(id, cancellationToken);

    public async Task<bool> UpdateIncomeRecordAsync(IncomeRecord record, CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        if (!Guid.TryParse(record.Id, out var id)) return false;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.IncomeRecords.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (entity is null) return false;
        entity.Source = record.Source.Trim(); entity.Classification = record.Classification; entity.Amount = record.Amount;
        entity.CurrencyId = await ResolveCurrencyIdAsync(db, record.Currency, cancellationToken);
        entity.Frequency = ParseFrequency(record.Frequency); entity.MineId = await ResolveMineIdAsync(db, userId, record.MineId, cancellationToken);
        entity.RecordDate = ParseDate(record.Date) ?? entity.RecordDate; entity.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken); return true;
    }

    public async Task<List<Expense>> GetExpensesAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await db.Expenses.AsNoTracking().Include(item => item.Currency)
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return records.Select(item => new Expense
        {
            Id = item.Id.ToString(),
            Name = item.Name,
            Amount = item.Amount,
            Currency = item.Currency?.Code ?? string.Empty,
            Frequency = FormatFrequency(item.Frequency)
        }).ToList();
    }

    public async Task AddExpenseAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        PublicInputValidator.ValidateDisplayText("Expense name", expense.Name);
        if (expense.Amount <= 0) throw new ArgumentException("Expense amount must be greater than zero.", nameof(expense));

        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currencyId = await ResolveCurrencyIdAsync(db, expense.Currency, cancellationToken);
        db.Expenses.Add(new ExpenseEntity
        {
            UserId = userId,
            Name = expense.Name.Trim(),
            Amount = expense.Amount,
            CurrencyId = currencyId,
            Frequency = ParseFrequency(expense.Frequency),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteExpenseAsync(string id, CancellationToken cancellationToken = default) =>
        await SoftDeleteAsync<ExpenseEntity>(id, cancellationToken);

    public async Task<bool> UpdateExpenseAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        if (!Guid.TryParse(expense.Id, out var id)) return false;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Expenses.SingleOrDefaultAsync(item => item.Id == id && item.UserId == userId, cancellationToken);
        if (entity is null) return false;
        entity.Name = expense.Name.Trim(); entity.Amount = expense.Amount;
        entity.CurrencyId = await ResolveCurrencyIdAsync(db, expense.Currency, cancellationToken);
        entity.Frequency = ParseFrequency(expense.Frequency); entity.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken); return true;
    }

    public async Task<List<Goal>> GetGoalsAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await db.Goals.AsNoTracking()
            .Include(item => item.Currency)
            .Include(item => item.MineLinks)
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.TargetDate)
            .ToListAsync(cancellationToken);
        return records.Select(item => new Goal
        {
            Id = item.Id.ToString(),
            Title = item.Title,
            Type = item.Type,
            Target = item.Target,
            Current = item.Current,
            Currency = item.Currency?.Code,
            TargetDate = item.TargetDate.ToString("yyyy-MM-dd"),
            Status = item.Status,
            LinkedMineIds = item.MineLinks.Select(link => link.MineId.ToString()).ToList()
        }).ToList();
    }

    public async Task AddGoalAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        PublicInputValidator.ValidateDisplayText("Goal title", goal.Title);
        if (goal.Target <= 0) throw new ArgumentException("Goal target must be greater than zero.", nameof(goal));

        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;
        int? currencyId = string.IsNullOrWhiteSpace(goal.Currency)
            ? null
            : await ResolveCurrencyIdAsync(db, goal.Currency, cancellationToken);
        var mineIds = await ResolveMineIdsAsync(db, userId, goal.LinkedMineIds, cancellationToken);
        var entity = new GoalEntity
        {
            UserId = userId,
            Title = goal.Title.Trim(),
            Type = goal.Type,
            Target = goal.Target,
            Current = goal.Current,
            CurrencyId = currencyId,
            TargetDate = ParseDate(goal.TargetDate) ?? throw new ArgumentException("A valid goal date is required.", nameof(goal)),
            Status = string.IsNullOrWhiteSpace(goal.Status) ? "not-started" : goal.Status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Goals.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var mineId in mineIds.Distinct())
        {
            db.GoalMines.Add(new GoalMineEntity
            {
                GoalId = entity.Id,
                MineId = mineId,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }

    public async Task<bool> DeleteGoalAsync(string id, CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        if (!Guid.TryParse(id, out var goalId)) return false;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var goal = await db.Goals.SingleOrDefaultAsync(item => item.Id == goalId && item.UserId == userId, cancellationToken);
        if (goal is null) return false;
        goal.IsDeleted = true;
        goal.DeletedAt = DateTimeOffset.UtcNow;
        var links = await db.GoalMines.Where(item => item.GoalId == goalId && item.UserId == userId).ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            link.IsDeleted = true;
            link.DeletedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateGoalAsync(Goal goal, CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        if (!Guid.TryParse(goal.Id, out var goalId)) return false;
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Goals.SingleOrDefaultAsync(item => item.Id == goalId && item.UserId == userId, cancellationToken);
        if (entity is null) return false;
        entity.Title = goal.Title.Trim(); entity.Type = goal.Type; entity.Target = goal.Target; entity.Current = goal.Current;
        entity.CurrencyId = string.IsNullOrWhiteSpace(goal.Currency) ? null : await ResolveCurrencyIdAsync(db, goal.Currency, cancellationToken);
        entity.TargetDate = ParseDate(goal.TargetDate) ?? entity.TargetDate; entity.Status = goal.Status; entity.ModifiedAt = DateTimeOffset.UtcNow;
        var links = await db.GoalMines
            .IgnoreQueryFilters()
            .Where(item => item.GoalId == goalId && item.UserId == userId)
            .ToListAsync(cancellationToken);
        var selectedMineIds = (await ResolveMineIdsAsync(db, userId, goal.LinkedMineIds, cancellationToken)).ToHashSet();
        foreach (var link in links)
        {
            if (selectedMineIds.Remove(link.MineId))
            {
                link.IsDeleted = false;
                link.DeletedAt = null;
            }
            else
            {
                link.IsDeleted = true;
                link.DeletedAt = DateTimeOffset.UtcNow;
            }
        }
        foreach (var mineId in selectedMineIds)
            db.GoalMines.Add(new GoalMineEntity { GoalId = goalId, MineId = mineId, UserId = userId, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(cancellationToken); return true;
    }

    private async Task<bool> SoftDeleteAsync<TEntity>(string id, CancellationToken cancellationToken)
        where TEntity : UserOwnedEntity
    {
        if (!Guid.TryParse(id, out var recordId)) return false;
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Set<TEntity>().SingleOrDefaultAsync(item => item.Id == recordId && item.UserId == userId, cancellationToken);
        if (entity is null) return false;
        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<int> ResolveCurrencyIdAsync(ApplicationDbContext db, string? code, CancellationToken cancellationToken)
    {
        var selectedCode = code;
        if (string.IsNullOrWhiteSpace(selectedCode))
        {
            var profile = await profileService.GetCurrentAsync(cancellationToken)
                ?? throw new InvalidOperationException("Complete your miner profile before adding financial records.");
            selectedCode = profile.CurrencyCode;
        }
        if (string.IsNullOrWhiteSpace(selectedCode))
            throw new InvalidOperationException("Select a base currency in your profile first.");

        var currencyId = await db.Currencies.Where(item => item.Code == selectedCode && item.IsActive)
            .Select(item => (int?)item.Id).SingleOrDefaultAsync(cancellationToken);
        return currencyId ?? throw new ArgumentException("The selected currency is not available.", nameof(code));
    }

    private static async Task<Guid?> ResolveMineIdAsync(ApplicationDbContext db, string userId, string? id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var mineId)) return null;
        var exists = await db.Mines.AnyAsync(item => item.Id == mineId && item.UserId == userId, cancellationToken);
        return exists ? mineId : throw new ArgumentException("The selected mine is not available.", nameof(id));
    }

    private static async Task<List<Guid>> ResolveMineIdsAsync(ApplicationDbContext db, string userId, IEnumerable<string>? ids, CancellationToken cancellationToken)
    {
        var parsed = (ids ?? []).Select(id => Guid.TryParse(id, out var value) ? value : (Guid?)null).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var count = await db.Mines.CountAsync(item => item.UserId == userId && parsed.Contains(item.Id), cancellationToken);
        return count == parsed.Count ? parsed : throw new ArgumentException("One or more selected mines are not available.", nameof(ids));
    }

    private async Task<string> RequireUserIdAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true
            ? state.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? state.User.FindFirstValue("sub")
                ?? throw new InvalidOperationException("The authenticated Public user has no identifier.")
            : throw new InvalidOperationException("An authenticated Public user is required.");
    }

    private static RecordFrequency ParseFrequency(string? frequency) => frequency?.Trim().ToLowerInvariant() switch
    {
        "monthly" => RecordFrequency.Monthly,
        "annual" or "annually" => RecordFrequency.Annual,
        "one-off" or "oneoff" => RecordFrequency.OneOff,
        _ => throw new ArgumentException("Frequency must be monthly, annual, or one-off.", nameof(frequency))
    };

    private static string FormatFrequency(RecordFrequency frequency) => frequency switch
    {
        RecordFrequency.Monthly => "monthly",
        RecordFrequency.Annual => "annual",
        RecordFrequency.OneOff => "one-off",
        _ => "monthly"
    };

    private static DateOnly? ParseDate(string? value) => DateOnly.TryParse(value, out var date) ? date : null;

    private static Burden MapBurden(BurdenEntity item) => new()
    {
        Id = item.Id.ToString(), Name = item.Name, Type = item.Type, Balance = item.Balance,
        OriginalAmount = item.OriginalAmount, InterestRate = item.InterestRate, MonthlyPayment = item.MonthlyPayment,
        Currency = item.Currency?.Code ?? string.Empty, LinkedMineId = item.LinkedMineId?.ToString(),
        MaturityDate = item.MaturityDate?.ToString("yyyy-MM-dd")
    };

    private static IncomeRecord MapIncome(IncomeRecordEntity item) => new()
    {
        Id = item.Id.ToString(), Source = item.Source, Classification = item.Classification, Amount = item.Amount,
        Currency = item.Currency?.Code ?? string.Empty, Frequency = FormatFrequency(item.Frequency),
        MineId = item.MineId?.ToString(), MineName = item.Mine?.Name, Date = item.RecordDate.ToString("yyyy-MM-dd")
    };
}
