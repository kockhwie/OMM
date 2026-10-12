**Decision Pathway: Implementation Plan**

Phases are ordered so each one ships on its own. Follow your existing rules: design first, no auto-run tests, no auto-commit, and shared logic in OMM.Shared.

**Phase 0: Bug fixes and hardening (~½ day)**

| **#** | **Task**                                                                                                                                                                                                 | **File**                                            |
| ----- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------- |
| 0.1   | Require login for AI classification. Anonymous users skip to the guided flow. This protects your Gemini key. Add a length cap (~500 chars) and a per-user cooldown (e.g. 10 calls/hour via IMemoryCache) | DecisionPathway.razor, GoogleAiIntentInterpreter.cs |
| 0.2   | Fix the classified/vague cards on Screen 1. Stay on Screen 1 when the result is isClassified or isVague, and only GoToScreen(2) on the Proceed button or a chip click                                    | InterpretQuestionAsync                              |
| 0.3   | ResetPathway should reset selectedAction to hold, and use one default objective in both the field initialiser and reset                                                                                  | DecisionPathway.razor                               |
| 0.4   | Button text is "Continue" when AiEnabled is false, and the label matches behaviour                                                                                                                       | Screen 1                                            |
| 0.5   | Map keys to labels in history (price_drop, income, hold, ...) through the engine's GetConcerns/Objectives lists, plus an action-label lookup                                                             | DecisionHistory.razor                               |
| 0.6   | Sanitise memberQuestion with PublicInputValidator before it goes into Notes                                                                                                                              | SavePlanAsync                                       |

**Done when:** no anonymous path reaches Gemini, and a full flow, reset, and flow again behaves identically.

**Phase 1: Make every step change the output (~2 days)**

This is the highest-value work.

**1.1 Impact map.** Make BuildImpactMap(category, concernKey, scopeKey, ...) vary by all three. Use a small data-driven table instead of a giant switch:

- Concern changes the emphasis: which node is "high" severity, plus the vulnerabilities and resilience lists.
- Scope changes the Corporate Impact node wording (single balance sheet vs sector margins vs portfolio weights).

**1.2 Action options.** EvaluateActionOptions(objective, concern) should reorder and score options, not just swap one sentence:

- Add Fit (High/Medium/Low) per action from an objective × action matrix.
- Sort by fit, and surface why it fits or doesn't (e.g. sell + income = low fit, "forfeits all dividends").

**1.3 Scenarios.** Use the actionMultiplier you already computed, but model position outcomes rather than per-share only:

- Input gains SharesOwned and CostBasis (optional).
- Per action, compute the remaining exposure after the action: hold 100%, trim 70%, sell 0%, accumulate 125%.
- Output per branch: price, dividend, and **RM impact on the position**, not just percent.
- sell should show realised P&L and zero future dividends, making the trade-off visible.

**1.4 Honest labelling.**

- Rename the badge to "Illustrative Scenarios".
- Replace the hardcoded +10%/-12%/×1.05/×0.90 with named constants in a ScenarioAssumptions record, shown in the UI ("Assumes ±…, adjust below").
- Optional later: derive them from historical volatility once you store price history.

**1.5 Tests** (OMM.Shared.Tests, you run them): action changes exposure, sell yields zero forward dividend, concern changes the severity ordering, stress adjustment shifts only the bear branch, and a zero price returns no scenarios.

**Phase 2: Portfolio awareness (~2–3 days)**

**2.1 Contract in Shared:**

csharp

public interface IPortfolioContextProvider {

Task&lt;PositionContext?&gt; GetPositionAsync(string userId, string stockCodeOrName, CancellationToken ct);

Task&lt;PortfolioSnapshot&gt; GetSnapshotAsync(string userId, CancellationToken ct);

}

public sealed record PositionContext(string StockCode, decimal Shares, decimal CostBasis,

decimal MarketValue, decimal WeightPct, decimal AnnualDividend);

**2.2 Implementation in Public**, using IMineRepository, matching on MineMetadata.StockCode for Stocks/REIT/ETF mines. Weight is the position's value divided by total valued mines.

**2.3 Route split**, following your Tools pattern:

- Extract the workspace into Components/Shared/DecisionPathwayContent.razor.
- /decision-pathway stays public (MainLayout). Anonymous users use manual inputs.
- /dashboard/decision-pathway uses DashboardLayout, \[Authorize\], and the sidebar entry. It auto-fills holdings.

**2.4 UI effects:**

- Step 3 shows "You hold 1,000 shares, 23% of your mines" when matched.
- concentration concern is flagged as relevant or irrelevant based on real weight (e.g. under 10% → note that it's not a concentration problem).
- Scenarios pre-fill shares and cost basis, with an override.

**Decision needed:** matching by free-text target ("Maybank (1155)") is brittle. Use StockSearchPicker in Step 3 instead of the raw text box so you get a real StockCode.

**Phase 3: Checkpoint follow-through (~1–2 days)**

**3.1 Schema.** Add to DecisionPathwayRecord: ReviewDate (DateOnly), TriggerPriceBelow?, TriggerPriceAbove?, Status (Open/Reviewed/Dismissed), ReviewedAt?, StockCode?. Add an EF migration that is safe against an existing DB.

**3.2 Engine.** GenerateCheckpoint returns a concrete ReviewDate (horizon → date) and suggested trigger prices derived from the reference price and the action (e.g. trim: below -8%, above +10%).

**3.3 Notifications.** Extend NotificationService.GenerateAsync with type decision, dedupe key decision-review:{id}:{date}, generated when ReviewDate is within 7 days or past due and the status is Open.

**3.4 History page.** Add a status badge, Due/Overdue highlighting, and "Mark reviewed" and "Dismiss" actions, plus a filter for Open.

**3.5 Settings.** Wire in the existing (currently dead) alert toggles on Settings.razor, adding a "Decision review reminders" switch.

**Phase 4: Wording and compliance (~½ day + legal)**

- Add a persistent disclaimer on Steps 6–8: "Educational framework, not investment advice."
- Rename actions to neutral wording: "Hold position", "Reduce exposure", "Exit position", "Add gradually", "Wait and review".
- Replace "Suitability" with "When this tends to fit".
- Remove directive phrasing ("Recommended only when…").
- Get a Malaysian lawyer's read on CMSL implications before public launch. This is a business risk, not a code task.

**Phase 5 (optional): Collapse the flow**

Once Phases 1–2 are in, Steps 2–4 can default from the signal and portfolio. Merge them into one "Frame it" screen with defaults pre-selected, taking the flow from 8 steps to about 5. Do this last, only after you've seen where users drop off.

**Order and effort**

| **Phase** | **Effort**   | **Depends on**                   |
| --------- | ------------ | -------------------------------- |
| 0         | 0.5d         | none                             |
| 1         | 2d           | none                             |
| 2         | 2–3d         | 1 (for position-based scenarios) |
| 3         | 1–2d         | migration                        |
| 4         | 0.5d + legal | none, can run anytime            |
| 5         | 1d           | 1, 2                             |

Ship 0 + 1 first. They fix the credibility gap, and nothing else matters if users see identical scenarios for Sell and Hold.

**Risks**

- **Scenario model creep:** keep it simple and clearly illustrative, with no pseudo-forecasting.
- **Stock matching:** names vs codes will bite you without the picker.
- **Migration drift:** use the drift-safe pattern from your AGENTS.md notes.
- **Gemini cost/abuse:** Phase 0.1 is not optional.