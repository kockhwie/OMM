using OMM.Shared.DecisionSupport;
using Xunit;

namespace OMM.Shared.Tests.DecisionSupport;

public class DeterministicDecisionEngineTests
{
    private readonly DeterministicDecisionEngine _engine = new();

    [Fact]
    public void BuildImpactMap_VariesByScope_ChangesCorporateImpactNode()
    {
        // Act
        var stockScope = _engine.BuildImpactMap(SignalCategory.IndividualStock, "price_drop", "individual_stock", "Maybank", null);
        var sectorScope = _engine.BuildImpactMap(SignalCategory.IndividualStock, "price_drop", "sector", "Maybank", null);
        var portfolioScope = _engine.BuildImpactMap(SignalCategory.IndividualStock, "price_drop", "portfolio", "Maybank", null);

        // Assert
        var stockCorpNode = stockScope.Nodes.First(n => n.Stage == "3. Corporate Impact");
        var sectorCorpNode = sectorScope.Nodes.First(n => n.Stage == "3. Corporate Impact");
        var portfolioCorpNode = portfolioScope.Nodes.First(n => n.Stage == "3. Corporate Impact");

        Assert.Contains("Balance Sheet", stockCorpNode.Title);
        Assert.Contains("Industry Cost", sectorCorpNode.Title);
        Assert.Contains("Portfolio Allocation", portfolioCorpNode.Title);
    }

    [Fact]
    public void BuildImpactMap_VariesByConcern_ChangesSeverityOrderingAndVulnerabilities()
    {
        // Act
        var dividendConcern = _engine.BuildImpactMap(SignalCategory.IndividualStock, "dividend", "individual_stock", "Maybank", null);
        var macroConcern = _engine.BuildImpactMap(SignalCategory.IndividualStock, "macro_shock", "individual_stock", "Maybank", null);

        // Assert
        var divCorpNode = dividendConcern.Nodes.First(n => n.Stage == "3. Corporate Impact");
        var macroTriggerNode = macroConcern.Nodes.First(n => n.Stage == "1. Trigger Event");

        Assert.Equal("high", divCorpNode.Severity);
        Assert.Equal("high", macroTriggerNode.Severity);

        Assert.Contains(dividendConcern.KeyVulnerabilities, v => v.Contains("Payout ratio", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(macroConcern.KeyVulnerabilities, v => v.Contains("foreign", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EvaluateActionOptions_IncomeObjective_RanksHoldHighestAndSellLowest()
    {
        // Act
        var options = _engine.EvaluateActionOptions("income", "dividend");

        // Assert
        Assert.NotEmpty(options);
        var topOption = options.First();
        var bottomOption = options.Last();

        Assert.Equal("hold", topOption.Key);
        Assert.Equal("High", topOption.Fit);

        Assert.Equal("sell", bottomOption.Key);
        Assert.Equal("Low", bottomOption.Fit);
        Assert.Contains("Forfeits", bottomOption.SuitabilityRationale, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EvaluateActionOptions_CapitalPreservationObjective_RanksSellAndTrimHighest()
    {
        // Act
        var options = _engine.EvaluateActionOptions("capital_preservation", "price_drop");

        // Assert
        var topOptions = options.Take(2).Select(o => o.Key).ToList();
        Assert.Contains("sell", topOptions);
        Assert.Contains("trim", topOptions);

        var sellOption = options.First(o => o.Key == "sell");
        Assert.Equal("High", sellOption.Fit);
    }

    [Fact]
    public void CalculateScenarios_SellAction_YieldsZeroForwardDividendAndZeroTotalReturn()
    {
        // Arrange
        var input = new ScenarioCalculationInput(10.00m, 0.50m);

        // Act
        var scenarios = _engine.CalculateScenarios(input, "sell");

        // Assert
        Assert.Equal(3, scenarios.Count);
        foreach (var branch in scenarios)
        {
            Assert.Equal(0m, branch.DividendForecastPerShare);
            Assert.Equal(0m, branch.EstimatedTotalReturnPercent);
            Assert.Contains("0% Exposure", branch.Label);
        }
    }

    [Fact]
    public void CalculateScenarios_TrimAction_YieldsProportionalDividendsAndReturn()
    {
        // Arrange
        var input = new ScenarioCalculationInput(10.00m, 1.00m);

        // Act
        var holdScenarios = _engine.CalculateScenarios(input, "hold");
        var trimScenarios = _engine.CalculateScenarios(input, "trim");

        // Assert
        var holdBull = holdScenarios.First(b => b.Key == "bull");
        var trimBull = trimScenarios.First(b => b.Key == "bull");

        Assert.True(trimBull.DividendForecastPerShare < holdBull.DividendForecastPerShare);
        Assert.True(trimBull.EstimatedTotalReturnPercent < holdBull.EstimatedTotalReturnPercent);
        Assert.Contains("70%", trimBull.Label);
    }

    [Fact]
    public void CalculateScenarios_StressAdjustment_ShiftsOnlyBearBranch()
    {
        // Arrange
        var normalInput = new ScenarioCalculationInput(10.00m, 0.50m, StressAdjustmentPercent: 0);
        var stressedInput = new ScenarioCalculationInput(10.00m, 0.50m, StressAdjustmentPercent: 10);

        // Act
        var normalBranches = _engine.CalculateScenarios(normalInput, "hold");
        var stressedBranches = _engine.CalculateScenarios(stressedInput, "hold");

        // Assert
        var normalBull = normalBranches.First(b => b.Key == "bull");
        var stressedBull = stressedBranches.First(b => b.Key == "bull");
        Assert.Equal(normalBull.TargetSharePrice, stressedBull.TargetSharePrice);

        var normalBear = normalBranches.First(b => b.Key == "bear");
        var stressedBear = stressedBranches.First(b => b.Key == "bear");
        Assert.True(stressedBear.TargetSharePrice < normalBear.TargetSharePrice);
    }

    [Fact]
    public void GenerateCheckpoint_CalculatesConcreteReviewDate_FromHorizon()
    {
        // Act
        var checkpoint = _engine.GenerateCheckpoint("MAYBANK", "trim", "3 months");

        // Assert
        Assert.NotNull(checkpoint.ReviewDate);
        var expectedApprox = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(3));
        Assert.Equal(expectedApprox.Year, checkpoint.ReviewDate.Value.Year);
        Assert.Equal(expectedApprox.Month, checkpoint.ReviewDate.Value.Month);
    }

    [Theory]
    [InlineData("trim", 10.00, 9.20, 11.00)]
    [InlineData("accumulate", 10.00, 9.00, 10.50)]
    [InlineData("hold", 10.00, 9.20, 11.50)]
    public void GenerateCheckpoint_CalculatesTriggerPrices_AccordingToAction(string action, decimal price, decimal expectedBelow, decimal expectedAbove)
    {
        // Act
        var checkpoint = _engine.GenerateCheckpoint("MAYBANK", action, "Several months", price);

        // Assert
        Assert.Equal(expectedBelow, checkpoint.SuggestedTriggerBelow);
        Assert.Equal(expectedAbove, checkpoint.SuggestedTriggerAbove);
    }

    [Fact]
    public void GenerateCheckpoint_SellAction_YieldsNullTriggerPrices()
    {
        // Act
        var checkpoint = _engine.GenerateCheckpoint("MAYBANK", "sell", "Several months", 10.00m);

        // Assert
        Assert.Null(checkpoint.SuggestedTriggerBelow);
        Assert.Null(checkpoint.SuggestedTriggerAbove);
    }
}
