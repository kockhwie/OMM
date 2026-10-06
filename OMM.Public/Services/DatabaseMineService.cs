using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Public.Validation;

namespace OMM.Public.Services;

public sealed class DatabaseMineService(
    IMineRepository repository,
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    IMinerProfileService profileService) 
{
    public async Task<List<Mine>> GetMinesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await repository.GetMinesAsync(cancellationToken);
        return entities.Select(MapMine).ToList();
    }

    public async Task<Mine?> GetMineByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return Guid.TryParse(id, out var mineId)
            ? (await repository.GetMineAsync(mineId, cancellationToken)) is { } entity
                ? MapMine(entity)
                : null
            : null;
    }

    public async Task AddMineAsync(Mine mine, CancellationToken cancellationToken = default)
    {
        PublicInputValidator.ValidateDisplayText("Mine name", mine.Name);
        PublicInputValidator.ValidateDisplayText("Holdings / details", mine.Holdings, optional: true);

        if (!FormatHelper.GetTypesForCategory(mine.Category).Contains(mine.Type))
        {
            throw new ArgumentException(
                $"'{FormatHelper.MineTypeLabel(mine.Type)}' is not a valid asset type for the '{FormatHelper.CategoryLabel(mine.Category)}' category.",
                nameof(mine));
        }

        var profile = await profileService.GetCurrentAsync(cancellationToken)
            ?? throw new InvalidOperationException("Complete your miner profile before adding a mine.");
        if (!profile.CurrencyId.HasValue || string.IsNullOrWhiteSpace(profile.CurrencyCode))
        {
            throw new InvalidOperationException("Choose a base currency in Settings before adding a Mine.");
        }

        var currencyCode = string.IsNullOrWhiteSpace(mine.Currency) ? profile.CurrencyCode : mine.Currency;
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            throw new InvalidOperationException("Select a base currency in your profile before adding a mine.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var currency = await db.Currencies.SingleOrDefaultAsync(
            item => item.Code == currencyCode && item.IsActive,
            cancellationToken);
        if (currency is null)
        {
            throw new ArgumentException("The selected currency is not available.", nameof(mine));
        }

        int? institutionId = null;
        if (!string.IsNullOrWhiteSpace(mine.Institution))
        {
            institutionId = await db.Institutions
                .Where(item => item.IsActive && item.InstitutionName_EN == mine.Institution)
                .Select(item => (int?)item.Id)
                .SingleOrDefaultAsync(cancellationToken);
            if (institutionId is null)
            {
                throw new ArgumentException("The selected institution is not available.", nameof(mine));
            }
        }

        var purchaseCost = mine.PurchaseCost;
        var growth = mine.CurrentValue - purchaseCost;
        var growthPct = purchaseCost > 0 ? Math.Round(growth / purchaseCost * 100m, 4) : 0m;
        Guid? linkedBurdenGuid = Guid.TryParse(mine.LinkedBurdenId, out var parsedBurden) ? parsedBurden : null;

        var addedEntity = await repository.AddMineAsync(new MineEntity
        {
            UserId = string.Empty,
            Name = mine.Name.Trim(),
            Category = mine.Category,
            Type = mine.Type,
            InstitutionId = institutionId,
            CurrencyId = currency.Id,
            CurrentValue = mine.CurrentValue,
            PurchaseCost = purchaseCost,
            Growth = growth,
            GrowthPct = growthPct,
            MonthlyIncome = mine.MonthlyIncome,
            Holdings = string.IsNullOrWhiteSpace(mine.Holdings) ? null : mine.Holdings.Trim(),
            MetadataJson = mine.Metadata is not null ? System.Text.Json.JsonSerializer.Serialize(mine.Metadata) : null,
            LinkedBurdenId = linkedBurdenGuid,
            Status = string.IsNullOrWhiteSpace(mine.Status) ? "active" : mine.Status,
            UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        }, cancellationToken);

        if (mine.Type == MineType.ForeignCurrency && mine.ForeignCurrencyTransactions is { Count: > 0 })
        {
            await AddForeignCurrencyTransactionsAsync(addedEntity.Id, mine.ForeignCurrencyTransactions, cancellationToken);
            await RecalculateForeignCurrencySummaryAsync(addedEntity.Id, cancellationToken);
        }
    }

    public async Task<bool> UpdateMineAsync(
        Mine mine,
        bool archiveRateToHistory = false,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(mine.Id, out var mineId))
        {
            return false;
        }

        PublicInputValidator.ValidateDisplayText("Mine name", mine.Name);
        PublicInputValidator.ValidateDisplayText("Holdings / details", mine.Holdings, optional: true);

        var existingEntity = await repository.GetMineAsync(mineId, cancellationToken);
        if (existingEntity is null)
        {
            return false;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        int? institutionId = null;
        if (!string.IsNullOrWhiteSpace(mine.Institution))
        {
            institutionId = await db.Institutions
                .Where(item => item.IsActive && item.InstitutionName_EN == mine.Institution)
                .Select(item => (int?)item.Id)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var purchaseCost = mine.PurchaseCost;
        var growth = mine.CurrentValue - purchaseCost;
        var growthPct = purchaseCost > 0 ? Math.Round(growth / purchaseCost * 100m, 4) : 0m;
        Guid? linkedBurdenGuid = Guid.TryParse(mine.LinkedBurdenId, out var parsedBurden) ? parsedBurden : null;
        var yieldHistory = archiveRateToHistory
            ? CreatePreviousRateSnapshot(existingEntity)
            : null;

        existingEntity.Name = mine.Name.Trim();
        existingEntity.Category = mine.Category;
        existingEntity.Type = mine.Type;
        existingEntity.InstitutionId = institutionId;
        existingEntity.CurrentValue = mine.CurrentValue;
        existingEntity.PurchaseCost = purchaseCost;
        existingEntity.Growth = growth;
        existingEntity.GrowthPct = growthPct;
        existingEntity.MonthlyIncome = mine.MonthlyIncome;
        existingEntity.Holdings = string.IsNullOrWhiteSpace(mine.Holdings) ? null : mine.Holdings.Trim();
        existingEntity.MetadataJson = mine.Metadata is not null ? System.Text.Json.JsonSerializer.Serialize(mine.Metadata) : null;
        existingEntity.LinkedBurdenId = linkedBurdenGuid;
        existingEntity.Status = string.IsNullOrWhiteSpace(mine.Status) ? "active" : mine.Status;
        existingEntity.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);

        var updated = await repository.UpdateMineAsync(existingEntity, yieldHistory, cancellationToken);
        if (updated is null)
        {
            return false;
        }

        if (mine.Type == MineType.ForeignCurrency && mine.ForeignCurrencyTransactions is { Count: > 0 })
        {
            await UpdateForeignCurrencyPurchaseAsync(mineId, mine.ForeignCurrencyTransactions, cancellationToken);
            await RecalculateForeignCurrencySummaryAsync(mineId, cancellationToken);
        }

        return true;
    }

    private static MineYieldHistoryEntity? CreatePreviousRateSnapshot(MineEntity existingEntity)
    {
        MineMetadata? metadata = null;
        if (!string.IsNullOrWhiteSpace(existingEntity.MetadataJson))
        {
            try
            {
                metadata = System.Text.Json.JsonSerializer.Deserialize<MineMetadata>(existingEntity.MetadataJson);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        var previousRate = metadata?.ExpectedDividendRate
            ?? metadata?.InterestRatePct
            ?? metadata?.DividendYieldPct;

        return previousRate is > 0
            ? new MineYieldHistoryEntity
            {
                UserId = string.Empty,
                EffectiveYear = DateTime.UtcNow.Year,
                RecordDate = DateOnly.FromDateTime(DateTime.UtcNow),
                RatePct = previousRate.Value,
                BalanceAtTime = existingEntity.CurrentValue,
                Notes = "Previous rate and balance before mine update"
            }
            : null;
    }

    public async Task<bool> DeleteMineAsync(string id, CancellationToken cancellationToken = default)
    {
        return Guid.TryParse(id, out var mineId)
            && await repository.SoftDeleteMineAsync(mineId, cancellationToken);
    }

    public async Task<SubMine?> AddPositionAsync(
        string mineId,
        SubMine position,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(mineId, out var parsedMineId))
        {
            return null;
        }

        PublicInputValidator.ValidateDisplayText("Position label", position.Label);
        var mine = await repository.GetMineAsync(parsedMineId, cancellationToken);
        if (mine is null)
        {
            return null;
        }

        var entity = await repository.AddPositionAsync(new MinePositionEntity
        {
            UserId = string.Empty,
            MineId = parsedMineId,
            Label = position.Label.Trim(),
            PositionType = InferPositionType(mine.Type, position),
            Principal = position.Principal,
            Rate = position.Rate,
            MaturityDate = ParseDate(position.MaturityDate),
            Quantity = position.Quantity,
            PurchasePrice = position.PurchasePrice
        }, cancellationToken);
        return MapPosition(entity);
    }

    public Task<bool> DeletePositionAsync(
        string mineId,
        string positionId,
        CancellationToken cancellationToken = default)
    {
        return Guid.TryParse(mineId, out var parsedMineId) && Guid.TryParse(positionId, out var parsedPositionId)
            ? repository.SoftDeletePositionAsync(parsedMineId, parsedPositionId, cancellationToken)
            : Task.FromResult(false);
    }

    public async Task<Burden?> GetLinkedBurdenAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(id, out var burdenId))
        {
            return null;
        }

        var profile = await profileService.GetCurrentAsync(cancellationToken);
        if (profile is null)
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Burdens
            .AsNoTracking()
            .Include(item => item.Currency)
            .SingleOrDefaultAsync(item => item.Id == burdenId && item.UserId == profile.UserId, cancellationToken);
        return entity is null ? null : new Burden
        {
            Id = entity.Id.ToString(),
            Name = entity.Name,
            Type = entity.Type,
            Balance = entity.Balance,
            OriginalAmount = entity.OriginalAmount,
            InterestRate = entity.InterestRate,
            MonthlyPayment = entity.MonthlyPayment,
            Currency = entity.Currency?.Code ?? string.Empty,
            LinkedMineId = entity.LinkedMineId?.ToString(),
            MaturityDate = entity.MaturityDate?.ToString("yyyy-MM-dd")
        };
    }

    private static Mine MapMine(MineEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        Name = entity.Name,
        Category = entity.Category,
        Type = entity.Type,
        Institution = entity.Institution?.InstitutionName_EN,
        Currency = entity.Currency?.Code ?? string.Empty,
        CurrentValue = entity.CurrentValue,
        PurchaseCost = entity.PurchaseCost,
        Growth = entity.Growth,
        GrowthPct = entity.GrowthPct,
        MonthlyIncome = entity.MonthlyIncome,
        Holdings = entity.Holdings,
        SubMines = entity.Positions.Select(MapPosition).ToList(),
        ForeignCurrencyTransactions = entity.ForeignCurrencyTransactions.Select(MapForeignCurrencyTransaction).ToList(),
        LinkedBurdenId = entity.LinkedBurdenId?.ToString(),
        Metadata = string.IsNullOrWhiteSpace(entity.MetadataJson)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<MineMetadata>(entity.MetadataJson),
        Status = entity.Status,
        UpdatedAt = entity.UpdatedOn.ToString("yyyy-MM-dd")
    };

    private static ForeignCurrencyTransaction MapForeignCurrencyTransaction(ForeignCurrencyTransactionEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        TransactionType = entity.TransactionType,
        TransactionDate = entity.TransactionDate,
        ForeignAmount = entity.ForeignAmount,
        MyrAmount = entity.MyrAmount,
        ExchangeRate = entity.ExchangeRate,
        FeesMyr = entity.FeesMyr,
        Notes = entity.Notes
    };

    public async Task<bool> AddForeignCurrencyTransactionAsync(
        string mineId,
        ForeignCurrencyTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(mineId, out var parsedMineId)) return false;
        ValidateForeignCurrencyTransaction(transaction);

        var profile = await profileService.GetCurrentAsync(cancellationToken);
        if (profile is null) return false;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mine = await db.Mines
            .Include(item => item.ForeignCurrencyTransactions)
            .SingleOrDefaultAsync(item => item.Id == parsedMineId && item.UserId == profile.UserId, cancellationToken);
        if (mine is null || mine.Type != MineType.ForeignCurrency) return false;

        db.ForeignCurrencyTransactions.Add(new ForeignCurrencyTransactionEntity
        {
            UserId = profile.UserId,
            MineId = mine.Id,
            TransactionType = transaction.TransactionType,
            TransactionDate = transaction.TransactionDate,
            ForeignAmount = transaction.ForeignAmount,
            MyrAmount = transaction.MyrAmount,
            ExchangeRate = transaction.ExchangeRate,
            FeesMyr = transaction.FeesMyr,
            Notes = transaction.Notes
        });

        await db.SaveChangesAsync(cancellationToken);
        RecalculateForeignCurrencyMine(mine);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task AddForeignCurrencyTransactionsAsync(
        Guid mineId,
        IEnumerable<ForeignCurrencyTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var profile = await profileService.GetCurrentAsync(cancellationToken)
            ?? throw new InvalidOperationException("Complete your miner profile before adding a foreign currency mine.");

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mine = await db.Mines
            .Include(item => item.ForeignCurrencyTransactions)
            .SingleAsync(item => item.Id == mineId && item.UserId == profile.UserId, cancellationToken);

        foreach (var transaction in transactions)
        {
            ValidateForeignCurrencyTransaction(transaction);
            db.ForeignCurrencyTransactions.Add(new ForeignCurrencyTransactionEntity
            {
                UserId = profile.UserId,
                MineId = mine.Id,
                TransactionType = transaction.TransactionType,
                TransactionDate = transaction.TransactionDate,
                ForeignAmount = transaction.ForeignAmount,
                MyrAmount = transaction.MyrAmount,
                ExchangeRate = transaction.ExchangeRate,
                FeesMyr = transaction.FeesMyr,
                Notes = transaction.Notes
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateForeignCurrencyPurchaseAsync(
        Guid mineId,
        IEnumerable<ForeignCurrencyTransaction> transactions,
        CancellationToken cancellationToken)
    {
        var purchase = transactions
            .Where(item => item.TransactionType == ForeignCurrencyTransactionType.Purchase)
            .OrderBy(item => item.TransactionDate)
            .FirstOrDefault();
        if (purchase is null)
        {
            return;
        }

        ValidateForeignCurrencyTransaction(purchase);

        var profile = await profileService.GetCurrentAsync(cancellationToken);
        if (profile is null)
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        ForeignCurrencyTransactionEntity? existingTransaction = null;

        if (Guid.TryParse(purchase.Id, out var transactionId))
        {
            existingTransaction = await db.ForeignCurrencyTransactions
                .SingleOrDefaultAsync(
                    item => item.Id == transactionId
                        && item.MineId == mineId
                        && item.UserId == profile.UserId
                        && !item.IsDeleted,
                    cancellationToken);
        }

        existingTransaction ??= await db.ForeignCurrencyTransactions
            .Where(item => item.MineId == mineId && item.UserId == profile.UserId && !item.IsDeleted)
            .OrderBy(item => item.TransactionDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingTransaction is null)
        {
            return;
        }

        existingTransaction.TransactionDate = purchase.TransactionDate;
        existingTransaction.ForeignAmount = purchase.ForeignAmount;
        existingTransaction.MyrAmount = purchase.MyrAmount;
        existingTransaction.ExchangeRate = purchase.ExchangeRate;
        existingTransaction.FeesMyr = purchase.FeesMyr;
        existingTransaction.Notes = purchase.Notes;

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecalculateForeignCurrencySummaryAsync(
        Guid mineId,
        CancellationToken cancellationToken)
    {
        var profile = await profileService.GetCurrentAsync(cancellationToken)
            ?? throw new InvalidOperationException("Complete your miner profile before updating a foreign currency mine.");

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mine = await db.Mines
            .Include(item => item.ForeignCurrencyTransactions)
            .SingleOrDefaultAsync(item => item.Id == mineId && item.UserId == profile.UserId, cancellationToken);
        if (mine is null)
        {
            return;
        }

        RecalculateForeignCurrencyMine(mine);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void RecalculateForeignCurrencyMine(MineEntity mine)
    {
        var transactions = mine.ForeignCurrencyTransactions;
        var foreignHeld = transactions.Sum(item => item.TransactionType == ForeignCurrencyTransactionType.Sale
            ? -item.ForeignAmount
            : item.ForeignAmount);
        var totalCost = transactions.Sum(item => item.TransactionType == ForeignCurrencyTransactionType.Sale
            ? -item.MyrAmount
            : item.MyrAmount + item.FeesMyr);
        var currentSellRate = !string.IsNullOrWhiteSpace(mine.MetadataJson)
            ? System.Text.Json.JsonSerializer.Deserialize<MineMetadata>(mine.MetadataJson)?.CurrentSellRate
            : null;

        mine.PurchaseCost = Math.Max(0, totalCost);
        if (currentSellRate is > 0)
        {
            mine.CurrentValue = Math.Round(foreignHeld / currentSellRate.Value, 2);
            mine.Growth = mine.CurrentValue - mine.PurchaseCost;
            mine.GrowthPct = mine.PurchaseCost > 0
            ? Math.Round(mine.Growth / mine.PurchaseCost * 100m, 4)
            : 0;
        }
        else
        {
            mine.CurrentValue = 0;
            mine.Growth = 0;
            mine.GrowthPct = 0;
        }
        mine.Holdings = $"{foreignHeld:N2} foreign units";
        mine.UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    private static void ValidateForeignCurrencyTransaction(ForeignCurrencyTransaction transaction)
    {
        if (transaction.TransactionDate == default) throw new ArgumentException("A transaction date is required.");
        if (transaction.ForeignAmount <= 0) throw new ArgumentOutOfRangeException(nameof(transaction.ForeignAmount));
        if (transaction.MyrAmount <= 0) throw new ArgumentOutOfRangeException(nameof(transaction.MyrAmount));
        if (transaction.ExchangeRate <= 0) throw new ArgumentOutOfRangeException(nameof(transaction.ExchangeRate));
        if (transaction.FeesMyr < 0) throw new ArgumentOutOfRangeException(nameof(transaction.FeesMyr));
    }

    public async Task<bool> RecordYieldDeclarationAsync(
        string mineId,
        int effectiveYear,
        DateOnly recordDate,
        decimal ratePct,
        decimal? declaredAmount = null,
        decimal? balanceAtTime = null,
        string? notes = null,
        bool createIncomeRecord = false,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(mineId, out var parsedMineId)) return false;
        if (effectiveYear is < 1 or > 9999) throw new ArgumentOutOfRangeException(nameof(effectiveYear));
        if (recordDate == default) throw new ArgumentException("A record date is required.", nameof(recordDate));
        if (ratePct <= 0) throw new ArgumentOutOfRangeException(nameof(ratePct), "Rate percentage must be greater than zero.");
        if (declaredAmount < 0 || balanceAtTime < 0) throw new ArgumentException("Amounts cannot be negative.");
        if (createIncomeRecord && (!declaredAmount.HasValue || declaredAmount <= 0))
        {
            throw new ArgumentException("A positive declared amount is required for the matching income record.", nameof(declaredAmount));
        }

        var profile = await profileService.GetCurrentAsync(cancellationToken);
        if (profile is null) return false;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mine = await db.Mines
            .SingleOrDefaultAsync(item => item.Id == parsedMineId && item.UserId == profile.UserId, cancellationToken);
        if (mine is null) return false;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.MineYieldHistories.Add(new MineYieldHistoryEntity
            {
                UserId = profile.UserId,
                MineId = mine.Id,
                EffectiveYear = effectiveYear,
                RecordDate = recordDate,
                RatePct = ratePct,
                DeclaredAmount = declaredAmount,
                BalanceAtTime = balanceAtTime,
                Notes = notes
            });

            if (createIncomeRecord)
            {
                db.IncomeRecords.Add(new IncomeRecordEntity
                {
                    UserId = profile.UserId,
                    Source = $"{mine.Name} rate declaration",
                    Classification = IncomeClass.PassiveMineGenerated,
                    Amount = declaredAmount!.Value,
                    CurrencyId = mine.CurrencyId,
                    Frequency = RecordFrequency.Annual,
                    MineId = mine.Id,
                    RecordDate = recordDate
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> RecordYieldHistoryAsync(
        string mineId,
        int effectiveYear,
        DateOnly recordDate,
        decimal ratePct,
        decimal? declaredAmount = null,
        decimal? balanceAtTime = null,
        string? notes = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(mineId, out var parsedMineId)) return false;
        var profile = await profileService.GetCurrentAsync(cancellationToken);
        if (profile is null) return false;

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var history = new MineYieldHistoryEntity
        {
            UserId = profile.UserId,
            MineId = parsedMineId,
            EffectiveYear = effectiveYear,
            RecordDate = recordDate,
            RatePct = ratePct,
            DeclaredAmount = declaredAmount,
            BalanceAtTime = balanceAtTime,
            Notes = notes
        };
        db.Set<MineYieldHistoryEntity>().Add(history);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<MineYieldHistoryEntity>> GetYieldHistoriesAsync(
        string mineId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(mineId, out var parsedMineId)) return [];
        var profile = await profileService.GetCurrentAsync(cancellationToken);
        if (profile is null) return [];

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<MineYieldHistoryEntity>()
            .AsNoTracking()
            .Where(h => h.MineId == parsedMineId && h.UserId == profile.UserId)
            .OrderByDescending(h => h.EffectiveYear)
            .ThenByDescending(h => h.RecordDate)
            .ToListAsync(cancellationToken);
    }

    private static SubMine MapPosition(MinePositionEntity entity) => new()
    {
        Id = entity.Id.ToString(),
        Label = entity.Label,
        Principal = entity.Principal,
        Rate = entity.Rate,
        MaturityDate = entity.MaturityDate?.ToString("yyyy-MM-dd"),
        Quantity = entity.Quantity,
        PurchasePrice = entity.PurchasePrice
    };

    private static PositionType InferPositionType(MineType type, SubMine position) => type switch
    {
        MineType.FixedDeposit => PositionType.FixedDeposit,
        MineType.Stocks or MineType.StocksUs
            or MineType.Reit or MineType.Etf
            or MineType.UnitTrustGeneral
            or MineType.UnitTrustAsb => PositionType.StockLot,
        MineType.PropertyResidential
            or MineType.PropertyCommercial => PositionType.PropertyDetail,
        MineType.Gold => PositionType.GoldLot,
        MineType.Silver => PositionType.SilverLot,
        _ => PositionType.Other
    };

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, out var date) ? date : null;
}
