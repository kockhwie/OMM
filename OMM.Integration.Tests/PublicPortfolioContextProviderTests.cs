using System.Text.Json;
using Microsoft.Extensions.Logging;
using OMM.Public.Data.Entities;
using OMM.Public.Models;
using OMM.Public.Services;
using OMM.Shared.DecisionSupport;
using Xunit;

namespace OMM.Integration.Tests;

public sealed class PublicPortfolioContextProviderTests
{
    private const string TestUserId = "user-1";
    private readonly ILogger<PortfolioContextProvider> _logger =
        new LoggerFactory().CreateLogger<PortfolioContextProvider>();

    [Fact]
    public async Task GetPositionAsync_WithExactStockCode_MatchesAndComputesAccurateMetricsAndWeight()
    {
        var maybankMeta = new MineMetadata
        {
            StockCode = "1155",
            SharesOwned = 1000,
            PurchasePricePerShare = 8.50m,
            CurrentSharePrice = 10.00m,
            DividendYieldPct = 6.0m
        };

        var pbbankMeta = new MineMetadata
        {
            StockCode = "1295",
            SharesOwned = 5000,
            CurrentSharePrice = 6.00m
        };

        var mines = new List<MineEntity>
        {
            new()
            {
                UserId = TestUserId,
                Name = "Maybank",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 10000m,
                PurchaseCost = 8500m,
                MonthlyIncome = 50m, // 50 * 12 = 600
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(maybankMeta)
            },
            new()
            {
                UserId = TestUserId,
                Name = "Public Bank",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 30000m,
                PurchaseCost = 25000m,
                MonthlyIncome = 100m,
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(pbbankMeta)
            },
            new()
            {
                UserId = TestUserId,
                Name = "Emergency FD",
                Category = MineCategory.CashAndDeposits,
                Type = MineType.FixedDeposit,
                CurrentValue = 10000m,
                PurchaseCost = 10000m,
                CurrencyId = 1,
                Status = "Active"
            }
        };

        var repo = new FakeMineRepository(mines);
        var provider = new PortfolioContextProvider(repo, _logger);

        var position = await provider.GetPositionAsync(TestUserId, "1155");

        Assert.NotNull(position);
        Assert.Equal("1155", position.StockCode);
        Assert.Equal("Maybank", position.StockName);
        Assert.Equal(1000m, position.Shares);
        Assert.Equal(8.50m, position.CostBasis);
        Assert.Equal(10000m, position.MarketValue);
        Assert.Equal(8500m, position.TotalCost);
        Assert.Equal(600m, position.AnnualDividend);
        // Total valued mines: 10,000 + 30,000 + 10,000 = 50,000. Weight = 10,000 / 50,000 = 20%
        Assert.Equal(20.00m, position.WeightPct);
    }

    [Fact]
    public async Task GetPositionAsync_WithCombinedStockNameAndCode_MatchesCorrectly()
    {
        var meta = new MineMetadata
        {
            StockCode = "1155",
            SharesOwned = 1200,
            PurchasePricePerShare = 9.00m,
            CurrentSharePrice = 10.50m
        };

        var mines = new List<MineEntity>
        {
            new()
            {
                UserId = TestUserId,
                Name = "Malayan Banking Berhad",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 12600m,
                PurchaseCost = 10800m,
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(meta)
            }
        };

        var repo = new FakeMineRepository(mines);
        var provider = new PortfolioContextProvider(repo, _logger);

        var position = await provider.GetPositionAsync(TestUserId, "Maybank (1155)");

        Assert.NotNull(position);
        Assert.Equal("1155", position.StockCode);
        Assert.Equal("Malayan Banking Berhad", position.StockName);
        Assert.Equal(1200m, position.Shares);
        Assert.Equal(9.00m, position.CostBasis);
        Assert.Equal(100.00m, position.WeightPct);
    }

    [Fact]
    public async Task GetPositionAsync_WithMultipleLots_AggregatesSharesAndCostBasis()
    {
        var lot1 = new MineMetadata
        {
            StockCode = "1155",
            SharesOwned = 1000,
            PurchasePricePerShare = 8.00m,
            CurrentSharePrice = 10.00m
        };
        var lot2 = new MineMetadata
        {
            StockCode = "1155",
            SharesOwned = 500,
            PurchasePricePerShare = 9.00m,
            CurrentSharePrice = 10.00m
        };

        var mines = new List<MineEntity>
        {
            new()
            {
                UserId = TestUserId,
                Name = "Maybank Lot 1",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 10000m,
                PurchaseCost = 8000m,
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(lot1)
            },
            new()
            {
                UserId = TestUserId,
                Name = "Maybank Lot 2",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 5000m,
                PurchaseCost = 4500m,
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(lot2)
            }
        };

        var repo = new FakeMineRepository(mines);
        var provider = new PortfolioContextProvider(repo, _logger);

        var position = await provider.GetPositionAsync(TestUserId, "1155");

        Assert.NotNull(position);
        Assert.Equal("1155", position.StockCode);
        Assert.Equal(1500m, position.Shares);
        Assert.Equal(15000m, position.MarketValue);
        // Total cost: 8000 + 4500 = 12500. Cost basis per share: 12500 / 1500 = 8.3333
        Assert.Equal(8.3333m, position.CostBasis);
        Assert.Equal(100.00m, position.WeightPct);
    }

    [Fact]
    public async Task GetPositionAsync_WhenRepositoryThrowsUnauthenticated_ReturnsNullSafely()
    {
        var repo = new FakeMineRepository([], shouldThrowAuthException: true);
        var provider = new PortfolioContextProvider(repo, _logger);

        var position = await provider.GetPositionAsync("user-anonymous", "1155");

        Assert.Null(position);
    }

    [Fact]
    public async Task GetPositionAsync_WithNonExistentStock_ReturnsNull()
    {
        var meta = new MineMetadata
        {
            StockCode = "1155",
            SharesOwned = 500
        };

        var mines = new List<MineEntity>
        {
            new()
            {
                UserId = TestUserId,
                Name = "Maybank",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 5000m,
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(meta)
            }
        };

        var repo = new FakeMineRepository(mines);
        var provider = new PortfolioContextProvider(repo, _logger);

        var position = await provider.GetPositionAsync(TestUserId, "AAPL");

        Assert.Null(position);
    }

    [Fact]
    public async Task GetSnapshotAsync_CalculatesTotalValuedMinesAndWeights()
    {
        var meta1 = new MineMetadata
        {
            StockCode = "1155",
            SharesOwned = 1000,
            CurrentSharePrice = 10.00m
        };
        var meta2 = new MineMetadata
        {
            StockCode = "1295",
            SharesOwned = 4000,
            CurrentSharePrice = 5.00m
        };

        var mines = new List<MineEntity>
        {
            new()
            {
                UserId = TestUserId,
                Name = "Maybank",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 10000m,
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(meta1)
            },
            new()
            {
                UserId = TestUserId,
                Name = "Public Bank",
                Category = MineCategory.Investments,
                Type = MineType.Stocks,
                CurrentValue = 20000m,
                CurrencyId = 1,
                Status = "Active",
                MetadataJson = JsonSerializer.Serialize(meta2)
            },
            new()
            {
                UserId = TestUserId,
                Name = "FD",
                Category = MineCategory.CashAndDeposits,
                Type = MineType.FixedDeposit,
                CurrentValue = 10000m,
                CurrencyId = 1,
                Status = "Active"
            }
        };

        var repo = new FakeMineRepository(mines);
        var provider = new PortfolioContextProvider(repo, _logger);

        var snapshot = await provider.GetSnapshotAsync(TestUserId);

        // Total: 10,000 + 20,000 + 10,000 = 40,000
        Assert.Equal(40000m, snapshot.TotalMinesValue);
        Assert.Equal(2, snapshot.StockPositionsCount);

        // Sorted by MarketValue descending
        Assert.Equal("1295", snapshot.Positions[0].StockCode);
        Assert.Equal(20000m, snapshot.Positions[0].MarketValue);
        Assert.Equal(50.00m, snapshot.Positions[0].WeightPct); // 20,000 / 40,000

        Assert.Equal("1155", snapshot.Positions[1].StockCode);
        Assert.Equal(10000m, snapshot.Positions[1].MarketValue);
        Assert.Equal(25.00m, snapshot.Positions[1].WeightPct); // 10,000 / 40,000
    }

    private sealed class FakeMineRepository(
        IReadOnlyList<MineEntity> mines,
        bool shouldThrowAuthException = false) : IMineRepository
    {
        public Task<IReadOnlyList<MineEntity>> GetMinesAsync(CancellationToken cancellationToken = default)
        {
            if (shouldThrowAuthException)
            {
                throw new InvalidOperationException("An authenticated Public user is required.");
            }
            return Task.FromResult(mines);
        }

        public Task<MineEntity?> GetMineAsync(Guid mineId, CancellationToken cancellationToken = default) =>
            Task.FromResult(mines.FirstOrDefault(m => m.Id == mineId));

        public Task<MineEntity> AddMineAsync(MineEntity mine, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MineEntity?> UpdateMineAsync(MineEntity mine, MineYieldHistoryEntity? yieldHistory = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> SoftDeleteMineAsync(Guid mineId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MinePositionEntity?> GetPositionAsync(Guid mineId, Guid positionId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MinePositionEntity> AddPositionAsync(MinePositionEntity position, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<MinePositionEntity?> UpdatePositionAsync(MinePositionEntity position, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> SoftDeletePositionAsync(Guid mineId, Guid positionId, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
