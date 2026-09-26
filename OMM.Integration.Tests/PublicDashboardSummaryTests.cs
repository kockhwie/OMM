using OMM.Public.Models;
using OMM.Public.Services;

namespace OMM.Integration.Tests;

public sealed class PublicDashboardSummaryTests
{
    [Fact]
    public void Empty_data_returns_zero_metrics()
    {
        var summary = DashboardQueryService.CalculateSummary([], [], [], [], true, "MYR");

        Assert.Equal(0, summary.TotalMines);
        Assert.Equal(0, summary.NetWealth);
        Assert.Equal(0, summary.FreedomRatio);
        Assert.Equal("MYR", summary.Currency);
    }

    [Fact]
    public void Annual_income_is_normalized_and_zero_expenses_are_safe()
    {
        var summary = DashboardQueryService.CalculateSummary(
            [new Mine { CurrentValue = 1000, PurchaseCost = 800, Growth = 200, Currency = "MYR" }],
            [],
            [new IncomeRecord { Classification = IncomeClass.PassiveMineGenerated, Amount = 1200, Frequency = "annual", Currency = "MYR" }],
            [], true, "MYR");

        Assert.Equal(100, summary.PassiveIncomeMonthly);
        Assert.Equal(100, summary.FreedomRatio);
        Assert.Equal(25, summary.TotalGrowthPct);
    }

    [Fact]
    public void Mixed_currencies_do_not_get_summed()
    {
        var summary = DashboardQueryService.CalculateSummary(
            [new Mine { CurrentValue = 1000, Currency = "MYR" }],
            [new Burden { Balance = 100, Currency = "USD" }],
            [], [], false, string.Empty);

        Assert.True(summary.HasMixedCurrencies);
        Assert.Equal(0, summary.TotalMines);
        Assert.Equal(0, summary.NetWealth);
        Assert.Empty(summary.Currency);
    }
}
