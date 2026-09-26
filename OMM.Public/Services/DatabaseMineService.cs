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

        var profile = await profileService.GetCurrentAsync(cancellationToken)
            ?? throw new InvalidOperationException("Complete your miner profile before adding a mine.");
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

        var purchaseCost = mine.PurchaseCost > 0 ? mine.PurchaseCost : mine.CurrentValue;
        var growth = mine.CurrentValue - purchaseCost;
        var growthPct = purchaseCost > 0 ? Math.Round(growth / purchaseCost * 100m, 4) : 0m;
        await repository.AddMineAsync(new MineEntity
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
            Status = string.IsNullOrWhiteSpace(mine.Status) ? "active" : mine.Status,
            UpdatedOn = DateOnly.FromDateTime(DateTime.UtcNow)
        }, cancellationToken);
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
        LinkedBurdenId = entity.LinkedBurdenId?.ToString(),
        Status = entity.Status,
        UpdatedAt = entity.UpdatedOn.ToString("yyyy-MM-dd")
    };

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
        MineType.Stocks or MineType.Reit or MineType.Funds => PositionType.StockLot,
        MineType.Property => PositionType.PropertyDetail,
        MineType.Gold => PositionType.GoldLot,
        MineType.Silver => PositionType.SilverLot,
        _ => PositionType.Other
    };

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, out var date) ? date : null;
}
