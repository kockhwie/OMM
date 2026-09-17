using OMM.Shared.Calculations;

namespace OMM.Shared.Tests.Calculations;

public class StockValuationCalculatorTests
{
    [Theory]
    [InlineData(10.00, 1.00, 10.0000)]
    [InlineData(10.50, 0.70, 15.0000)]
    [InlineData(5.25, 0.35, 15.0000)]
    public void CalculatePE_ValidInputs_ReturnsExpectedRatio(double price, double eps, double expected)
    {
        var result = StockValuationCalculator.CalculatePE((decimal)price, (decimal)eps);
        Assert.NotNull(result);
        Assert.Equal((decimal)expected, result.Value);
    }

    [Theory]
    [InlineData(null, 1.00)]
    [InlineData(10.00, null)]
    [InlineData(null, null)]
    [InlineData(10.00, 0.00)]
    [InlineData(10.00, -1.50)]
    [InlineData(0.00, 1.00)]
    [InlineData(-5.00, 1.00)]
    public void CalculatePE_InvalidOrNegativeOrZeroInputs_ReturnsNull(object? priceObj, object? epsObj)
    {
        decimal? price = priceObj is null ? null : Convert.ToDecimal(priceObj);
        decimal? eps = epsObj is null ? null : Convert.ToDecimal(epsObj);

        var result = StockValuationCalculator.CalculatePE(price, eps);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(10.00, 5.00, 2.0000)]
    [InlineData(8.40, 7.00, 1.2000)]
    [InlineData(3.50, 1.75, 2.0000)]
    public void CalculatePB_ValidInputs_ReturnsExpectedRatio(double price, double nta, double expected)
    {
        var result = StockValuationCalculator.CalculatePB((decimal)price, (decimal)nta);
        Assert.NotNull(result);
        Assert.Equal((decimal)expected, result.Value);
    }

    [Theory]
    [InlineData(null, 5.00)]
    [InlineData(10.00, null)]
    [InlineData(null, null)]
    [InlineData(10.00, 0.00)]
    [InlineData(10.00, -2.50)]
    [InlineData(0.00, 5.00)]
    [InlineData(-10.00, 5.00)]
    public void CalculatePB_InvalidOrNegativeOrZeroInputs_ReturnsNull(object? priceObj, object? ntaObj)
    {
        decimal? price = priceObj is null ? null : Convert.ToDecimal(priceObj);
        decimal? nta = ntaObj is null ? null : Convert.ToDecimal(ntaObj);

        var result = StockValuationCalculator.CalculatePB(price, nta);
        Assert.Null(result);
    }

    [Theory]
    [InlineData(10.00, 0.50, 5.0000)]
    [InlineData(7.00, 0.35, 5.0000)]
    [InlineData(2.50, 0.10, 4.0000)]
    [InlineData(10.00, 0.00, 0.0000)]
    public void CalculateDividendYield_ValidInputs_ReturnsExpectedPercentage(double price, double dps, double expected)
    {
        var result = StockValuationCalculator.CalculateDividendYield((decimal)price, (decimal)dps);
        Assert.NotNull(result);
        Assert.Equal((decimal)expected, result.Value);
    }

    [Theory]
    [InlineData(null, 0.50)]
    [InlineData(10.00, null)]
    [InlineData(null, null)]
    [InlineData(0.00, 0.50)]
    [InlineData(-5.00, 0.50)]
    [InlineData(10.00, -0.20)]
    public void CalculateDividendYield_InvalidOrNegativeInputs_ReturnsNull(object? priceObj, object? dpsObj)
    {
        decimal? price = priceObj is null ? null : Convert.ToDecimal(priceObj);
        decimal? dps = dpsObj is null ? null : Convert.ToDecimal(dpsObj);

        var result = StockValuationCalculator.CalculateDividendYield(price, dps);
        Assert.Null(result);
    }
}
