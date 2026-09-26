using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using OMM.Public.Data;
using OMM.Public.Data.Entities;

namespace OMM.Public.Services;

public sealed class MineRepository(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    AuthenticationStateProvider authenticationStateProvider) : IMineRepository
{
    public async Task<IReadOnlyList<MineEntity>> GetMinesAsync(CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Mines
            .AsNoTracking()
            .Include(mine => mine.Institution)
            .Include(mine => mine.Currency)
            .Include(mine => mine.Positions)
            .Where(mine => mine.UserId == userId)
            .OrderBy(mine => mine.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<MineEntity?> GetMineAsync(Guid mineId, CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Mines
            .AsNoTracking()
            .Include(mine => mine.Institution)
            .Include(mine => mine.Currency)
            .Include(mine => mine.Positions)
            .Where(mine => mine.UserId == userId && mine.Id == mineId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<MineEntity> AddMineAsync(
        MineEntity mine,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        mine.UserId = userId;
        mine.Id = Guid.NewGuid();
        mine.CreatedAt = DateTimeOffset.UtcNow;
        mine.IsDeleted = false;
        db.Mines.Add(mine);
        await db.SaveChangesAsync(cancellationToken);
        return mine;
    }

    public async Task<MineEntity?> UpdateMineAsync(
        MineEntity mine,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Mines.SingleOrDefaultAsync(
            item => item.UserId == userId && item.Id == mine.Id,
            cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.Name = mine.Name;
        existing.Category = mine.Category;
        existing.Type = mine.Type;
        existing.InstitutionId = mine.InstitutionId;
        existing.CurrencyId = mine.CurrencyId;
        existing.CurrentValue = mine.CurrentValue;
        existing.PurchaseCost = mine.PurchaseCost;
        existing.Growth = mine.Growth;
        existing.GrowthPct = mine.GrowthPct;
        existing.MonthlyIncome = mine.MonthlyIncome;
        existing.Holdings = mine.Holdings;
        existing.Status = mine.Status;
        existing.UpdatedOn = mine.UpdatedOn;
        existing.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> SoftDeleteMineAsync(Guid mineId, CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mine = await db.Mines
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == mineId, cancellationToken);
        if (mine is null)
        {
            return false;
        }

        mine.IsDeleted = true;
        mine.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MinePositionEntity?> GetPositionAsync(
        Guid mineId,
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.MinePositions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.MineId == mineId && item.Id == positionId,
            cancellationToken);
    }

    public async Task<MinePositionEntity> AddPositionAsync(
        MinePositionEntity position,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mineExists = await db.Mines.AnyAsync(
            mine => mine.UserId == userId && mine.Id == position.MineId,
            cancellationToken);
        if (!mineExists)
        {
            throw new KeyNotFoundException("The selected mine was not found for the current user.");
        }

        position.UserId = userId;
        position.Id = Guid.NewGuid();
        position.CreatedAt = DateTimeOffset.UtcNow;
        position.IsDeleted = false;
        db.MinePositions.Add(position);
        await db.SaveChangesAsync(cancellationToken);
        return position;
    }

    public async Task<MinePositionEntity?> UpdatePositionAsync(
        MinePositionEntity position,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.MinePositions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.MineId == position.MineId && item.Id == position.Id,
            cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.Label = position.Label;
        existing.PositionType = position.PositionType;
        existing.Principal = position.Principal;
        existing.Rate = position.Rate;
        existing.MaturityDate = position.MaturityDate;
        existing.Quantity = position.Quantity;
        existing.PurchasePrice = position.PurchasePrice;
        existing.StockId = position.StockId;
        existing.ModifiedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> SoftDeletePositionAsync(
        Guid mineId,
        Guid positionId,
        CancellationToken cancellationToken = default)
    {
        var userId = await RequireUserIdAsync();
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var position = await db.MinePositions.SingleOrDefaultAsync(
            item => item.UserId == userId && item.MineId == mineId && item.Id == positionId,
            cancellationToken);
        if (position is null)
        {
            return false;
        }

        position.IsDeleted = true;
        position.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<string> RequireUserIdAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("An authenticated Public user is required.");
        }

        return state.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? state.User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("The authenticated Public user has no identifier.");
    }
}
