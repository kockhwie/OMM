namespace OMM.Shared.DecisionSupport;

public interface IDeterministicDecisionEngine
{
    SignalTemplate MatchSignalFromAi(InquiryIntentResult intent, IReadOnlyList<SignalTemplate> signals);
    IReadOnlyList<MemberConcernOption> GetConcerns();
    IReadOnlyList<ScopeOption> GetScopes();
    IReadOnlyList<ObjectiveOption> GetObjectives();
    ImpactMapResult BuildImpactMap(SignalCategory category, string concernKey, string scopeKey, string? security, string? sector);
    IReadOnlyList<ActionOption> EvaluateActionOptions(string objectiveKey, string concernKey);
    IReadOnlyList<ScenarioBranch> CalculateScenarios(ScenarioCalculationInput input, string actionKey);
    MonitoringCheckpoint GenerateCheckpoint(string securityOrSector, string actionKey, string horizon);
}

public sealed class DeterministicDecisionEngine : IDeterministicDecisionEngine
{
    private static readonly IReadOnlyList<MemberConcernOption> Concerns =
    [
        new("price_drop", "Price moved sharply", "A sudden rise or fall has changed your valuation buffer.", "ti ti-chart-candle", "tone-red"),
        new("dividend", "Dividend or income sustainability", "A distribution, payout ratio, or ex-date needs examination.", "ti ti-building-bank", "tone-gold"),
        new("macro_shock", "Macro or policy event", "A central bank decision, inflation, or geopolitical shift.", "ti ti-world", "tone-blue"),
        new("concentration", "Portfolio concentration", "A single position has grown too large relative to total capital.", "ti ti-chart-pie", "tone-purple"),
        new("opportunity", "Potential buying opportunity", "Prices look discounted and you want to test whether to accumulate.", "ti ti-bulb", "tone-green")
    ];

    private static readonly IReadOnlyList<ScopeOption> Scopes =
    [
        new("individual_stock", "Individual Stock", "Single company performance, earnings, and dividend schedule.", "ti ti-building-bank", "stock"),
        new("sector", "Sector Level", "Industry-wide headwinds, margin trends, and regulatory mandates.", "ti ti-category", "sector"),
        new("malaysian_market", "Malaysian Market (KLCI)", "Domestic economic data, BNM monetary policy, and Ringgit currency.", "ti ti-flag", "domestic"),
        new("global_economy", "Global Economy", "US Fed policy, global trade tariffs, commodity price shifts.", "ti ti-globe", "global"),
        new("portfolio", "Entire Portfolio", "Overall asset allocation, position weights, cash reserves.", "ti ti-briefcase", "portfolio")
    ];

    private static readonly IReadOnlyList<ObjectiveOption> Objectives =
    [
        new("capital_preservation", "Preserve Capital", "Shield against major drawdown; prioritize defensive positioning.", "ti ti-shield-check", "Protection"),
        new("income", "Maximize & Stabilize Income", "Protect dividend yield and ensure ongoing cash distributions.", "ti ti-coin", "Cash flow"),
        new("balanced", "Balanced Realignment", "Trim extreme risk while retaining upside participation.", "ti ti-scale", "Equilibrium"),
        new("opportunity", "Accumulate Quality", "Deploy capital methodically when quality companies are discounted.", "ti ti-trending-up", "Value capture"),
        new("patient_monitor", "Patient Observation", "Establish clear rules and avoid emotional reactionary trading.", "ti ti-clock-pause", "Discipline")
    ];

    public SignalTemplate MatchSignalFromAi(InquiryIntentResult intent, IReadOnlyList<SignalTemplate> signals)
    {
        var category = intent.SignalType switch
        {
            "stock_event" => SignalCategory.IndividualStock,
            "sector_event" => SignalCategory.Sector,
            "market_event" => SignalCategory.MalaysianMarket,
            "economic_event" => SignalCategory.GlobalEconomy,
            "portfolio_decision" => SignalCategory.PortfolioDecision,
            "opportunity" => SignalCategory.Opportunity,
            _ => SignalCategory.PortfolioDecision
        };

        if (!string.IsNullOrWhiteSpace(intent.MentionedSecurity))
        {
            var stockMatch = signals.FirstOrDefault(signal =>
                signal.DefaultSecurity.Contains(intent.MentionedSecurity, StringComparison.OrdinalIgnoreCase));
            if (stockMatch is not null)
            {
                return stockMatch;
            }
        }

        var categoryMatch = signals.FirstOrDefault(signal => signal.Category == category);
        return categoryMatch ?? new SignalTemplate(
            $"member-question-{Guid.NewGuid():N}",
            category,
            string.IsNullOrWhiteSpace(intent.EventName) ? "Your question" : intent.EventName,
            intent.Summary,
            intent.Summary,
            intent.MentionedSecurity ?? string.Empty,
            intent.MentionedSector ?? string.Empty,
            "macro_shock",
            category switch
            {
                SignalCategory.IndividualStock => "individual_stock",
                SignalCategory.Sector => "sector",
                SignalCategory.MalaysianMarket => "malaysian_market",
                SignalCategory.GlobalEconomy => "global_economy",
                _ => "portfolio"
            },
            "balanced",
            isActive: false);
    }

    public IReadOnlyList<MemberConcernOption> GetConcerns() => Concerns;
    public IReadOnlyList<ScopeOption> GetScopes() => Scopes;
    public IReadOnlyList<ObjectiveOption> GetObjectives() => Objectives;

    public ImpactMapResult BuildImpactMap(
        SignalCategory category,
        string concernKey,
        string scopeKey,
        string? security,
        string? sector)
    {
        var targetName = !string.IsNullOrWhiteSpace(security) ? security : (!string.IsNullOrWhiteSpace(sector) ? sector : "Selected Position");

        return category switch
        {
            SignalCategory.GlobalEconomy => new ImpactMapResult(
                $"Global policy shift ripples through foreign fund allocations and currency valuations, influencing {targetName}.",
                "Foreign institutional capital reallocation across emerging market equities and bond yields.",
                "Valuation multiple contraction or expansion, alongside domestic interest rate expectation adjustments.",
                [
                    new("1. Trigger Event", "Global Monetary Policy Shift", "External interest rate adjustment or global trade friction.", "high", "tone-red"),
                    new("2. Transmission", "Cross-Border Capital Flows", "Currency exchange fluctuations and sovereign bond yield repricing.", "moderate", "tone-gold"),
                    new("3. Corporate Impact", $"{targetName} Balance Sheet", "Borrowing cost sensitivity and net interest margin / export competitiveness.", "moderate", "tone-blue"),
                    new("4. Market Pricing", "Equity Risk Premium Shift", "Short-term valuation multiple compression, even when core operational earnings remain intact.", "low", "tone-green")
                ],
                ["Foreign portfolio outflows during risk-off episodes", "Higher refinancing costs on USD-denominated debt"],
                ["Strong domestic institutional fund support (EPF, PNB, KWAP)", "Healthy operating cash flows and balance sheet liquidity"]),

            SignalCategory.Sector => new ImpactMapResult(
                $"Industry-level dynamics influence pricing power and cost structures across {targetName}.",
                "Direct changes in sector-wide input costs, regulatory pricing, or competitive pressure.",
                "Divergence between top-tier industry leaders and weaker marginal producers.",
                [
                    new("1. Trigger Event", "Sector Cost or Demand Shift", "Industry pricing or regulatory adjustment.", "high", "tone-gold"),
                    new("2. Transmission", "Margin Compression / Expansion", "Impact on operating margins across sector participants.", "moderate", "tone-blue"),
                    new("3. Corporate Impact", "Earnings & Dividend Buffer", "Capacity of companies to maintain dividend payouts without debt funding.", "moderate", "tone-purple"),
                    new("4. Market Pricing", "Relative Valuation Repricing", "Sector multiple realigns with long-term average historical multiples.", "low", "tone-green")
                ],
                ["Input cost spikes that cannot be passed through to customers", "Intense price competition shrinking gross margins"],
                ["Dominant market share with pricing power", "Low capital expenditure requirements"]),

            SignalCategory.PortfolioDecision => new ImpactMapResult(
                "Position weight changes reshape risk concentration and cash drag for the entire portfolio.",
                "Concentration risk amplifies downside exposure to single-company negative surprises.",
                "Opportunity cost of holding idle cash versus loss of capital flexibility during pullbacks.",
                [
                    new("1. Trigger Event", "Asset Allocation Imbalance", "Position size grew beyond prudent individual risk parameters.", "high", "tone-purple"),
                    new("2. Transmission", "Volatility Amplification", "Portfolio total return becomes excessively dependent on one ticker's fortune.", "moderate", "tone-gold"),
                    new("3. Corporate Impact", "Cash Flow Timing", "Dividend receipt schedule becomes concentrated in specific months.", "low", "tone-blue"),
                    new("4. Market Pricing", "Portfolio Resilience", "Diversified portfolios recover faster from localized sector disruptions.", "low", "tone-green")
                ],
                ["Single black-swan shock affecting disproportionate share of capital", "Emotional bias delaying rebalancing"],
                ["Rebalancing captures profits and locks in income", "Staggered cash reserves protect peace of mind"]),

            SignalCategory.Opportunity => new ImpactMapResult(
                $"Price discount creates an attractive entry window for {targetName} if core fundamentals remain solid.",
                "Market sentiment drives share price below historical median valuation multiples.",
                "Forward dividend yield increases as price contracts, providing a downside margin of safety.",
                [
                    new("1. Trigger Event", "Price Pullback / Sentiment Dip", "Broad market selling pressure or short-term earnings miss.", "moderate", "tone-green"),
                    new("2. Transmission", "Valuation Multiple Discount", "Price-to-earnings or price-to-book falls to historical lower quartile.", "low", "tone-blue"),
                    new("3. Corporate Impact", "Cash Flow & Dividend Health", "Company continues generating positive free cash flow to back distributions.", "low", "tone-purple"),
                    new("4. Market Pricing", "Yield Support Floor", "Elevated dividend yield attracts patient institutional and retail buyers.", "high", "tone-gold")
                ],
                ["Value trap risk if earnings drop permanently rather than cyclically", "Further short-term price weakness before recovery"],
                ["Sustainable free cash flow covering dividends by >1.2x", "History of steady recovery across prior cycles"]),

            _ => new ImpactMapResult(
                $"Specific corporate or market signal alters the short-term risk/reward balance for {targetName}.",
                "Direct impact on dividend eligibility, revenue trajectory, or capital expenditures.",
                "Market pricing incorporates new risk premium into immediate share price.",
                [
                    new("1. Trigger Event", "Company or Market Announcement", "Dividend ex-date, quarterly results, or key corporate event.", "high", "tone-blue"),
                    new("2. Transmission", "Cash Flow Realization", "Cash distribution or earnings revision takes effect.", "moderate", "tone-gold"),
                    new("3. Corporate Impact", "Balance Sheet & Retained Capital", "Cash leaves corporate balance sheet to pay shareholders.", "low", "tone-purple"),
                    new("4. Market Pricing", "Ex-Date Price Readjustment", "Share price adjusts downwards by dividend amount on ex-date morning.", "low", "tone-green")
                ],
                ["Post-dividend price drift if market sentiment is weak", "Short-term capital loss exceeding payout amount"],
                ["Consistent multi-year dividend track record", "Strong capital adequacy and banking/utility franchise"])
        };
    }

    public IReadOnlyList<ActionOption> EvaluateActionOptions(string objectiveKey, string concernKey)
    {
        return
        [
            new(
                "hold",
                "Hold and Reinvest",
                "Keep existing share count, collect ongoing dividends, and ignore short-term market noise.",
                "Income Focus",
                "tag-blue",
                ["Preserves eligibility for upcoming dividends", "Avoids transaction fees and selling at an inopportune moment", "Allows compounding through dividend reinvestment"],
                ["Exposed to further downside if macro sentiment worsens", "Requires emotional patience during drawdowns"],
                objectiveKey == "income" ? "Highest alignment with income objectives and dividend compounding." : "Suitable if you have a multi-year horizon and low liquidity needs."),

            new(
                "trim",
                "Trim Partially (20%–35%)",
                "Realize a portion of capital into cash while retaining core exposure for distributions.",
                "Balanced Defensive",
                "tag-gold",
                ["Reduces downside dollar exposure immediately", "Creates cash reserves to redeploy if prices drop further", "Maintains partial dividend income from remaining shares"],
                ["Cuts future dividend cash flow proportionately", "Possible regret if the share price rebounds swiftly"],
                objectiveKey is "balanced" or "capital_preservation" ? "Strongest balance between risk reduction and income continuity." : "Provides peace of mind when position weight feels uncomfortable."),

            new(
                "sell",
                "Sell and Move to Cash",
                "Liquidate position completely to lock in capital and eliminate downside volatility.",
                "Full Defensive",
                "tag-red",
                ["Complete protection against further price declines", "Frees up 100% of capital for risk-free cash yields or new opportunities", "Eliminates monitoring stress"],
                ["Forfeits all future dividend income", "Locks in any unrealized paper loss permanently", "Requires finding a better replacement asset"],
                objectiveKey == "capital_preservation" ? "Direct path to capital preservation if structural conviction has been lost." : "Appropriate only if company fundamentals have broken down permanently."),

            new(
                "accumulate",
                "Accumulate on the Dip",
                "Deploy staggered capital in tranches to lower average cost per share and lift portfolio yield.",
                "Opportunity Capture",
                "tag-green",
                ["Enhances long-term yield on cost", "Lowers breakeven purchase price across the position", "Capitalizes on temporary market overreaction"],
                ["Increases capital at risk if share price continues to decline", "Depletes cash reserves prematurely if done in a single lump sum"],
                objectiveKey == "opportunity" ? "High alignment with value-oriented and income-boosting strategies." : "Recommended only when dividend safety is confirmed by operating cash flow."),

            new(
                "monitor",
                "Set a Checkpoint & Wait",
                "Establish explicit measurable trigger rules and commit to making no trade today.",
                "Disciplined Patience",
                "tag-purple",
                ["Prevents impulsive emotional trading", "Allows market reaction to settle before committing capital", "Completely free of transaction costs"],
                ["Does not change existing risk exposure today", "May miss a short-lived recovery bounce"],
                objectiveKey == "patient_monitor" ? "Ideal choice when immediate data is noisy and no emergency exit is required." : "Best default when uncertain: let clear price or date triggers guide the next step.")
        ];
    }

    public IReadOnlyList<ScenarioBranch> CalculateScenarios(
        ScenarioCalculationInput input,
        string actionKey)
    {
        var price = Math.Max(input.CurrentSharePrice, 0.01m);
        var div = Math.Max(input.AnnualDividendPerShare, 0m);

        // Adjust parameters based on selected action
        var actionMultiplier = actionKey switch
        {
            "sell" => 0m,
            "trim" => 0.7m,
            "accumulate" => 1.25m,
            _ => 1m
        };

        // Bull Branch: +8% to +14% price movement + resilient dividend
        var bullPriceChange = 0.10m;
        var bullPrice = Math.Round(price * (1m + bullPriceChange), 2);
        var bullDiv = Math.Round(div * 1.05m, 2);
        var bullTotalReturn = price > 0 ? Math.Round(((bullPrice - price) + bullDiv) / price * 100m, 1) : 0m;

        // Base Branch: Steady price ±1.5% + standard dividend
        var basePriceChange = 0.00m;
        var basePrice = Math.Round(price * (1m + basePriceChange), 2);
        var baseDiv = Math.Round(div, 2);
        var baseTotalReturn = price > 0 ? Math.Round(((basePrice - price) + baseDiv) / price * 100m, 1) : 0m;

        // Bear Branch: -10% to -15% price compression + stress dividend (-10%)
        var bearPriceChange = -0.12m - (input.StressAdjustmentPercent / 100m);
        var bearPrice = Math.Round(price * (1m + bearPriceChange), 2);
        var bearDiv = Math.Round(div * 0.90m, 2);
        var bearTotalReturn = price > 0 ? Math.Round(((bearPrice - price) + bearDiv) / price * 100m, 1) : 0m;

        return
        [
            new(
                "bull",
                "Bullish Recovery Path",
                $"Target RM{bullPrice:F2} (+{bullPriceChange * 100:F0}%)",
                "tone-green",
                "ti ti-trending-up",
                bullPriceChange * 100m,
                bullPrice,
                bullDiv,
                bullTotalReturn,
                "Market fears subside as macro data stabilizes. Payout confirmed and share price recovers toward upper range.",
                "Earnings meet expectations and sector multiples expand back to historical median.",
                "Quarterly net profit growth, foreign fund inflows, institutional accumulation."),

            new(
                "base",
                "Base Steady Path",
                $"Target RM{basePrice:F2} (Neutral)",
                "tone-blue",
                "ti ti-minus",
                basePriceChange * 100m,
                basePrice,
                baseDiv,
                baseTotalReturn,
                "Price movements remain contained in a range. Dividend distribution provides the primary source of positive return.",
                "Operations proceed as guided with modest top-line growth offset by steady funding costs.",
                "Ex-dividend price recovery speed, loan growth pace, domestic consumer demand."),

            new(
                "bear",
                "Bearish Stress Path",
                $"Target RM{bearPrice:F2} ({bearPriceChange * 100:F0}%)",
                "tone-red",
                "ti ti-trending-down",
                bearPriceChange * 100m,
                bearPrice,
                bearDiv,
                bearTotalReturn,
                "Persistent macro headwinds or rate pressures drag sentiment lower. Share price drops to key historical support.",
                "Sustained margin pressure or broader regional equity market sell-off.",
                "Breach of support levels, asset quality / non-performing loan spikes, dividend payout reduction.")
        ];
    }

    public MonitoringCheckpoint GenerateCheckpoint(
        string securityOrSector,
        string actionKey,
        string horizon)
    {
        var target = !string.IsNullOrWhiteSpace(securityOrSector) ? securityOrSector : "Selected position";

        return actionKey switch
        {
            "sell" => new(
                $"Cash Reallocation Checkpoint for {target}",
                "Review available fixed income yields vs potential market re-entry opportunities.",
                horizon,
                "Risk-free yield rate & KLCI index valuation multiple",
                "Deploy into conservative dividend stalwarts if market discount widens by >10%."),

            "trim" => new(
                $"Partial Trim Evaluation Checkpoint for {target}",
                "Assess whether retained 70% exposure feels comfortable after price volatility settles.",
                horizon,
                "Share price stability above support & dividend confirmation",
                "If price rebounds past target, retain remaining stake; if it drops below support, pause further sales."),

            "accumulate" => new(
                $"Tranche Execution Checkpoint for {target}",
                "Verify that first accumulation tranche was completed and evaluate second tranche trigger.",
                horizon,
                "Price holding support level & confirmed dividend yield >6.0%",
                "Execute tranche 2 only if fundamental cash flow remains intact."),

            _ => new(
                $"Hold & Dividend Review Checkpoint for {target}",
                "Check post-dividend share price performance and ex-date price recovery duration.",
                horizon,
                "Ex-dividend price recovery & next quarterly earnings announcement",
                "If share price recovers within 14 days, maintain holding; if price slips >8%, revisit trim scenario.")
        };
    }
}
