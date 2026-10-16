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

        // 1. Determine base summary, direct, and indirect effect by Signal Category
        var (summary, direct, indirect, triggerTitle, triggerDesc, triggerTone, transTitle, transDesc, transTone) = category switch
        {
            SignalCategory.GlobalEconomy => (
                $"Global policy shift ripples through foreign fund allocations and currency valuations, influencing {targetName}.",
                "Foreign institutional capital reallocation across emerging market equities and bond yields.",
                "Valuation multiple contraction or expansion, alongside domestic interest rate expectation adjustments.",
                "Global Monetary Policy Shift", "External interest rate adjustment or global trade friction.", "tone-red",
                "Cross-Border Capital Flows", "Currency exchange fluctuations and sovereign bond yield repricing.", "tone-gold"
            ),
            SignalCategory.Sector => (
                $"Industry-level dynamics influence pricing power and cost structures across {targetName}.",
                "Direct changes in sector-wide input costs, regulatory pricing, or competitive pressure.",
                "Divergence between top-tier industry leaders and weaker marginal producers.",
                "Sector Cost or Demand Shift", "Industry pricing, margin trends, or regulatory adjustment.", "tone-gold",
                "Margin Compression / Expansion", "Impact on operating margins across sector participants.", "tone-blue"
            ),
            SignalCategory.PortfolioDecision => (
                "Position weight changes reshape risk concentration and cash drag for the entire portfolio.",
                "Concentration risk amplifies downside exposure to single-company negative surprises.",
                "Opportunity cost of holding idle cash versus loss of capital flexibility during pullbacks.",
                "Asset Allocation Imbalance", "Position size grew beyond prudent individual risk parameters.", "tone-purple",
                "Volatility Amplification", "Portfolio total return becomes excessively dependent on one ticker's fortune.", "tone-gold"
            ),
            SignalCategory.Opportunity => (
                $"Price discount creates an attractive entry window for {targetName} if core fundamentals remain solid.",
                "Market sentiment drives share price below historical median valuation multiples.",
                "Forward dividend yield increases as price contracts, providing a downside margin of safety.",
                "Price Pullback / Sentiment Dip", "Broad market selling pressure or short-term earnings miss.", "tone-green",
                "Valuation Multiple Discount", "Price-to-earnings or price-to-book falls to historical lower quartile.", "tone-blue"
            ),
            SignalCategory.MalaysianMarket => (
                $"Domestic macro shifts influence local institutional liquidity and policy sentiment across {targetName}.",
                "Changes in Bank Negara policy rates, Ringgit stability, or government fiscal announcements.",
                "Domestic liquidity reallocation between fixed deposits, bonds, and equities.",
                "Domestic Macro or Policy Shift", "BNM OPR rate decision, subsidy rationalization, or budget initiatives.", "tone-green",
                "Domestic Liquidity Transmission", "Local bank lending conditions and institutional pension fund flows.", "tone-blue"
            ),
            _ => (
                $"Specific corporate or market signal alters the short-term risk/reward balance for {targetName}.",
                "Direct impact on dividend eligibility, revenue trajectory, or capital expenditures.",
                "Market pricing incorporates new risk premium into immediate share price.",
                "Company or Market Announcement", "Dividend ex-date, quarterly results, or key corporate event.", "tone-blue",
                "Cash Flow Realization", "Cash distribution or earnings revision takes effect.", "tone-gold"
            )
        };

        // 2. Customize Stage 3 (Corporate Impact) by Scope:
        var (corpTitle, corpDesc, corpTone) = scopeKey switch
        {
            "sector" => (
                $"{targetName} Industry Cost & Margins",
                "Sector-wide margin compression, competitive supply dynamics, and peer regulatory compliance costs.",
                "tone-purple"
            ),
            "malaysian_market" => (
                "Domestic Market & BNM Transmission",
                "Ringgit currency sensitivity, domestic banking system liquidity, and Malaysian retail/institutional demand.",
                "tone-blue"
            ),
            "global_economy" => (
                "Global Capital Flows & Trade Exposure",
                "USD financing costs, cross-border supply chain dependencies, and global macro demand elasticity.",
                "tone-blue"
            ),
            "portfolio" => (
                "Portfolio Allocation & Cash Buffer",
                "Total portfolio capital at risk, single-asset concentration drag, and available liquid cash reserves.",
                "tone-purple"
            ),
            _ => (
                $"{targetName} Balance Sheet & Margins",
                "Borrowing cost sensitivity, debt service coverage, and capacity to sustain dividend payouts from operating cash flow.",
                "tone-blue"
            )
        };

        // 3. Customize Stage 4 (Market Pricing)
        var marketTitle = "Equity Risk Premium Shift";
        var marketDesc = "Short-term valuation multiple compression, even when core operational earnings remain intact.";
        var marketTone = "tone-green";

        // 4. Determine node severity and key lists based on Concern:
        string trigSeverity = "moderate", transSeverity = "moderate", corpSeverity = "moderate", marketSeverity = "low";

        var (vulnerabilities, resilience) = concernKey switch
        {
            "price_drop" => (
                (IReadOnlyList<string>)[
                    "Margin call liquidations and stop-loss cascading selling pressure",
                    "Short-term valuation multiple compression overshooting underlying company fundamentals"
                ],
                (IReadOnlyList<string>)[
                    "Operating free cash flow completely unaffected by daily secondary-market ticker volatility",
                    "Discounted share price lifts forward dividend yield, creating an institutional yield floor"
                ]
            ),
            "dividend" => (
                [
                    "Payout ratio exceeding underlying free cash flow generation",
                    "Debt refinancing or capex taking priority over discretionary shareholder distributions"
                ],
                [
                    "Retained earnings cushion and conservative historical board payout track record",
                    "Steady non-cyclical customer cash receipts maintaining distribution coverage above 1.2x"
                ]
            ),
            "macro_shock" => (
                [
                    "Broad emerging market risk-off capital flight driven by foreign institutional accounts",
                    "Higher debt service costs on floating-rate borrowings or USD-denominated obligations"
                ],
                [
                    "Strong sovereign and domestic pension fund (EPF, PNB, KWAP) buying on market dips",
                    "Net cash balance sheet or low leverage shielding from interest rate spikes"
                ]
            ),
            "concentration" => (
                [
                    "Single-company adverse surprise causing outsized portfolio drawdown",
                    "Emotional hesitation to take profits and rebalance capital away from a winning position"
                ],
                [
                    "High conviction in compounding fundamentals of core winner",
                    "Staggered profit-taking rules systematically restoring risk balance"
                ]
            ),
            "opportunity" => (
                [
                    "Premature capital deployment if sector downgrade or price correction has further to run",
                    "Value trap risk if earnings power has deteriorated permanently rather than cyclically"
                ],
                [
                    "Tranche-based dollar-cost averaging systematically lowering average entry cost",
                    "High dividend yield on cost locking in superior long-term cash-on-cash return"
                ]
            ),
            _ => (
                [
                    "Post-dividend price drift if market sentiment is weak",
                    "Short-term capital loss exceeding payout amount"
                ],
                [
                    "Consistent multi-year dividend track record",
                    "Strong capital adequacy and resilient operating cash flows"
                ]
            )
        };

        // Assign severity emphasis based on concern
        switch (concernKey)
        {
            case "macro_shock":
                trigSeverity = "high";
                transSeverity = "high";
                corpSeverity = "moderate";
                marketSeverity = "moderate";
                break;
            case "dividend":
                trigSeverity = "moderate";
                transSeverity = "moderate";
                corpSeverity = "high";
                marketSeverity = "moderate";
                break;
            case "concentration":
                trigSeverity = "moderate";
                transSeverity = "high";
                corpSeverity = "moderate";
                marketSeverity = "moderate";
                break;
            case "opportunity":
                trigSeverity = "low";
                transSeverity = "moderate";
                corpSeverity = "moderate";
                marketSeverity = "high";
                marketTitle = "Yield Support Floor & Value Capture";
                marketDesc = "Elevated dividend yield and discounted valuation attract patient long-term buyers.";
                break;
            case "price_drop":
            default:
                trigSeverity = "moderate";
                transSeverity = "moderate";
                corpSeverity = "moderate";
                marketSeverity = "high";
                marketTitle = "Market Pricing & Sentiment Reaction";
                marketDesc = "Short-term valuation multiple repricing reflects immediate sentiment rather than normalized value.";
                break;
        }

        var nodes = new List<ImpactTransmissionNode>
        {
            new("1. Trigger Event", triggerTitle, triggerDesc, trigSeverity, triggerTone),
            new("2. Transmission", transTitle, transDesc, transSeverity, transTone),
            new("3. Corporate Impact", corpTitle, corpDesc, corpSeverity, corpTone),
            new("4. Market Pricing", marketTitle, marketDesc, marketSeverity, marketTone)
        };

        return new ImpactMapResult(summary, direct, indirect, nodes, vulnerabilities, resilience);
    }

    public IReadOnlyList<ActionOption> EvaluateActionOptions(string objectiveKey, string concernKey)
    {
        var allActions = new List<(string Key, string Title, string Desc, string Tag, string Tone, string[] Adv, string[] Trades, string DefaultRationale)>
        {
            ("hold", "Hold and Reinvest", "Keep existing share count, collect ongoing dividends, and ignore short-term market noise.", "Income Focus", "tag-blue",
                ["Preserves eligibility for upcoming dividends", "Avoids transaction fees and selling at an inopportune moment", "Allows compounding through dividend reinvestment"],
                ["Exposed to further downside if macro sentiment worsens", "Requires emotional patience during drawdowns"],
                "Suitable if you have a multi-year horizon and low liquidity needs."),

            ("trim", "Trim Partially (20%–35%)", "Realize a portion of capital into cash while retaining core exposure for distributions.", "Balanced Defensive", "tag-gold",
                ["Reduces downside dollar exposure immediately", "Creates cash reserves to redeploy if prices drop further", "Maintains partial dividend income from remaining shares"],
                ["Cuts future dividend cash flow proportionately", "Possible regret if the share price rebounds swiftly"],
                "Provides peace of mind when position weight feels uncomfortable."),

            ("sell", "Sell and Move to Cash", "Liquidate position completely to lock in capital and eliminate downside volatility.", "Full Defensive", "tag-red",
                ["Complete protection against further price declines", "Frees up 100% of capital for risk-free cash yields or new opportunities", "Eliminates monitoring stress"],
                ["Forfeits all future dividend income", "Locks in any unrealized paper loss permanently", "Requires finding a better replacement asset"],
                "Appropriate only if company fundamentals have broken down permanently."),

            ("accumulate", "Accumulate on the Dip", "Deploy staggered capital in tranches to lower average cost per share and lift portfolio yield.", "Opportunity Capture", "tag-green",
                ["Enhances long-term yield on cost", "Lowers breakeven purchase price across the position", "Capitalizes on temporary market overreaction"],
                ["Increases capital at risk if share price continues to decline", "Depletes cash reserves prematurely if done in a single lump sum"],
                "Recommended only when dividend safety is confirmed by operating cash flow."),

            ("monitor", "Set a Checkpoint & Wait", "Establish explicit measurable trigger rules and commit to making no trade today.", "Disciplined Patience", "tag-purple",
                ["Prevents impulsive emotional trading", "Allows market reaction to settle before committing capital", "Completely free of transaction costs"],
                ["Does not change existing risk exposure today", "May miss a short-lived recovery bounce"],
                "Best default when uncertain: let clear price or date triggers guide the next step.")
        };

        var scored = allActions.Select(action =>
        {
            var key = action.Key;
            int score = 50;
            string fit = "Medium";
            string rationale = action.DefaultRationale;

            switch (objectiveKey)
            {
                case "capital_preservation":
                    if (key == "sell") { score = 95; fit = "High"; rationale = "Direct path to capital preservation: eliminates drawdown risk completely."; }
                    else if (key == "trim") { score = 85; fit = "High"; rationale = "Strong balance: locks in capital defensively while retaining upside participation."; }
                    else if (key == "monitor") { score = 65; fit = "Medium"; rationale = "Disciplined compromise: establishes stop-loss checkpoint rules without selling today."; }
                    else if (key == "hold") { score = 40; fit = "Low"; rationale = "Lower fit: leaves entire capital position exposed to potential further drawdown."; }
                    else if (key == "accumulate") { score = 15; fit = "Low"; rationale = "Contradicts capital preservation: adding capital increases downside risk."; }
                    break;

                case "income":
                    if (key == "hold") { score = 95; fit = "High"; rationale = "Highest fit for income: preserves full dividend entitlement and ongoing compounding."; }
                    else if (key == "accumulate") { score = 85; fit = "High"; rationale = "Boosts future income: buying at discounted prices lifts overall dividend yield on cost."; }
                    else if (key == "trim") { score = 60; fit = "Medium"; rationale = "Moderate fit: reduces forward dividend income by 25%–35%, but provides cash cushion."; }
                    else if (key == "monitor") { score = 50; fit = "Medium"; rationale = "Neutral fit: maintains current dividend flow while observing sustainability."; }
                    else if (key == "sell") { score = 10; fit = "Low"; rationale = "Lowest fit for income: forfeits 100% of all future dividends and income streams."; }
                    break;

                case "balanced":
                    if (key == "trim") { score = 95; fit = "High"; rationale = "Ideal fit for balanced objective: reduces risk exposure while preserving dividend continuity."; }
                    else if (key == "hold") { score = 80; fit = "High"; rationale = "Good fit: retains core compounding position if long-term business model remains solid."; }
                    else if (key == "monitor") { score = 70; fit = "Medium"; rationale = "Sensible choice: pauses trades until clearer intermediate price action emerges."; }
                    else if (key == "accumulate") { score = 60; fit = "Medium"; rationale = "Selective fit: suitable only in disciplined, small tranches during market dips."; }
                    else if (key == "sell") { score = 25; fit = "Low"; rationale = "Overly aggressive: liquidating completely forfeits recovery upside and dividend yield."; }
                    break;

                case "opportunity":
                    if (key == "accumulate") { score = 95; fit = "High"; rationale = "Highest fit for opportunity: methodically acquires quality assets at discounted valuation multiples."; }
                    else if (key == "hold") { score = 80; fit = "High"; rationale = "Solid fit: stands firm without panic-selling, allowing price recovery to unfold."; }
                    else if (key == "monitor") { score = 65; fit = "Medium"; rationale = "Patience fit: monitors price action for a clear technical or valuation support floor."; }
                    else if (key == "trim") { score = 35; fit = "Low"; rationale = "Counter-productive: selling during an attractive valuation window locks in lower prices."; }
                    else if (key == "sell") { score = 10; fit = "Low"; rationale = "Opposite of opportunity capture: permanently exits at market lows."; }
                    break;

                case "patient_monitor":
                default:
                    if (key == "monitor") { score = 95; fit = "High"; rationale = "Highest fit: establishes objective trigger conditions and eliminates emotional knee-jerk trading."; }
                    else if (key == "hold") { score = 85; fit = "High"; rationale = "Natural partner to monitoring: lets current investments work quietly without friction."; }
                    else if (key == "trim") { score = 60; fit = "Medium"; rationale = "Acceptable defensive hedge if position exceeds personal risk comfort."; }
                    else if (key == "accumulate") { score = 30; fit = "Low"; rationale = "Premature: deploys fresh capital before monitoring triggers confirm a bottom."; }
                    else if (key == "sell") { score = 20; fit = "Low"; rationale = "Impulsive exit: liquidates before predefined review horizons have played out."; }
                    break;
            }

            // Concern-specific modifier
            if (concernKey == "concentration" && key == "trim")
            {
                score += 15;
                fit = "High";
                rationale = "Directly resolves concentration concern by banking profits and reducing single-stock weight.";
            }
            else if (concernKey == "dividend" && key == "sell")
            {
                rationale += " Forfeits all upcoming dividend ex-date distributions.";
            }
            else if (concernKey == "price_drop" && key == "accumulate" && objectiveKey == "opportunity")
            {
                rationale = "Capitalizes on price drop: lowers average cost basis and increases forward yield.";
            }

            return new
            {
                Option = new ActionOption(
                    key,
                    action.Title,
                    action.Desc,
                    action.Tag,
                    action.Tone,
                    action.Adv,
                    action.Trades,
                    rationale,
                    fit),
                Score = score
            };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Option)
        .ToList();

        return scored;
    }

    public IReadOnlyList<ScenarioBranch> CalculateScenarios(
        ScenarioCalculationInput input,
        string actionKey)
    {
        var price = Math.Max(input.CurrentSharePrice, 0.01m);
        var div = Math.Max(input.AnnualDividendPerShare, 0m);

        // Adjust parameters and labels based on selected action
        var (actionMultiplier, actionTag) = actionKey switch
        {
            "sell" => (0m, "0% Exposure (Cash)"),
            "trim" => (0.7m, "70% Remaining Exposure"),
            "accumulate" => (1.25m, "125% Accumulated Exposure"),
            _ => (1m, "100% Retained Exposure")
        };

        // Standard scenario price variations
        const decimal bullPriceChange = 0.10m;
        const decimal basePriceChange = 0.00m;
        var bearPriceChange = -0.12m - (input.StressAdjustmentPercent / 100m);

        // Bull Branch (+10% market recovery)
        var bullMarketPrice = Math.Round(price * (1m + bullPriceChange), 2);
        var bullPrice = actionMultiplier == 0m ? price : bullMarketPrice;
        var bullDiv = actionMultiplier == 0m ? 0m : Math.Round(div * 1.05m * actionMultiplier, 2);
        var bullTotalReturn = actionMultiplier == 0m
            ? 0m
            : Math.Round((((bullMarketPrice - price) * actionMultiplier) + bullDiv) / price * 100m, 1);
        var bullDesc = actionMultiplier switch
        {
            0m => "Position was liquidated to cash. Complete downside protection, but forfeits the +10% price rebound and forward dividends.",
            0.7m => "30% locked into cash buffer. Remaining 70% stake participates in recovery (+7% gain) and captures partial dividends.",
            1.25m => "Accumulated tranches amplify gains on recovery (+12.5% gain) and expand forward dividend income.",
            _ => "Market fears subside as macro data stabilizes. Payout confirmed and share price recovers toward upper range."
        };

        // Base Branch (Neutral)
        var baseMarketPrice = Math.Round(price * (1m + basePriceChange), 2);
        var basePrice = actionMultiplier == 0m ? price : baseMarketPrice;
        var baseDiv = actionMultiplier == 0m ? 0m : Math.Round(div * actionMultiplier, 2);
        var baseTotalReturn = actionMultiplier == 0m
            ? 0m
            : Math.Round((((baseMarketPrice - price) * actionMultiplier) + baseDiv) / price * 100m, 1);
        var baseDesc = actionMultiplier switch
        {
            0m => "Capital held in cash. No equity price volatility and zero forward dividends.",
            0.7m => "70% exposure provides steady dividend distribution while 30% cash provides liquidity buffer.",
            1.25m => "Accumulated position collects higher total dividend stream while waiting for price catalyst.",
            _ => "Price movements remain contained in a range. Dividend distribution provides the primary source of positive return."
        };

        // Bear Branch (Stress test)
        var bearMarketPrice = Math.Round(price * (1m + bearPriceChange), 2);
        var bearPrice = actionMultiplier == 0m ? price : bearMarketPrice;
        var bearDiv = actionMultiplier == 0m ? 0m : Math.Round(div * 0.90m * actionMultiplier, 2);
        var bearTotalReturn = actionMultiplier == 0m
            ? 0m
            : Math.Round((((bearMarketPrice - price) * actionMultiplier) + bearDiv) / price * 100m, 1);
        var bearDesc = actionMultiplier switch
        {
            0m => "Complete protection: cash position successfully shielded 100% of capital from the price drawdown and dividend cut.",
            0.7m => "Cushioned drawdown: 30% cash cushion limits portfolio loss to only 70% of the market decline.",
            1.25m => "Stress drawdown is amplified by the 125% accumulated exposure if price breaks below support before stabilizing.",
            _ => "Persistent macro headwinds or rate pressures drag sentiment lower. Share price drops to key historical support."
        };

        return
        [
            new(
                "bull",
                "Bullish Recovery Path",
                $"Target RM{bullMarketPrice:F2} (+{bullPriceChange * 100:F0}%) · {actionTag}",
                "tone-green",
                "ti ti-trending-up",
                bullPriceChange * 100m,
                bullPrice,
                bullDiv,
                bullTotalReturn,
                bullDesc,
                "Earnings meet expectations and sector multiples expand back to historical median.",
                "Quarterly net profit growth, foreign fund inflows, institutional accumulation."),

            new(
                "base",
                "Base Steady Path",
                $"Target RM{baseMarketPrice:F2} (Neutral) · {actionTag}",
                "tone-blue",
                "ti ti-minus",
                basePriceChange * 100m,
                basePrice,
                baseDiv,
                baseTotalReturn,
                baseDesc,
                "Operations proceed as guided with modest top-line growth offset by steady funding costs.",
                "Ex-dividend price recovery speed, loan growth pace, domestic consumer demand."),

            new(
                "bear",
                "Bearish Stress Path",
                $"Target RM{bearMarketPrice:F2} ({bearPriceChange * 100:F0}%) · {actionTag}",
                "tone-red",
                "ti ti-trending-down",
                bearPriceChange * 100m,
                bearPrice,
                bearDiv,
                bearTotalReturn,
                bearDesc,
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
