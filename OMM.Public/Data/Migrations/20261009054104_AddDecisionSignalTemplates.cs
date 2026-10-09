using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionSignalTemplates : Migration
    {
        private static readonly string[] SignalTemplateIndexColumns = ["IsActive", "SortOrder"];
        private static readonly string[] SignalTemplateSeedColumns = ["Id", "Category", "Title", "Description", "SampleQuery", "DefaultSecurity", "DefaultSector", "DefaultConcern", "DefaultScope", "DefaultObjective", "IsActive", "SortOrder"];
        private static readonly object[,] SignalTemplateSeedValues =
        {
            { "stock-maybank-dividend", "IndividualStock", "Maybank (1155) · Dividend ex-date & margin shift", "Evaluate income yield versus short-term share price compression after a distribution.", "Maybank dividend announcement ex-date next week. Should I hold for the payout or trim?", "Maybank (1155)", "Financial Services", "dividend", "individual_stock", "income", true, 10 },
            { "stock-tenaga-tariff", "IndividualStock", "Tenaga Nasional (5347) · Grid capex & tariff review", "Regulatory tariff mechanism and energy transition capital expenditures.", "Tenaga announced new grid modernization capex. What does this mean for earnings?", "Tenaga Nasional (5347)", "Utilities", "opportunity", "individual_stock", "opportunity", true, 20 },
            { "stock-inari-tech", "IndividualStock", "Inari Amertron (0166) · OSAT chip cycle demand", "Semiconductor assembly demand swings and global smartphone order cycles.", "Inari dropped after earnings guidance. Is this a temporary cyclical dip?", "Inari Amertron (0166)", "Technology", "price_drop", "individual_stock", "opportunity", true, 30 },
            { "sector-banking-nim", "Sector", "Banking sector · Net interest margin & deposit competition", "Funding costs increase as promotional fixed deposits reprice across lenders.", "Malaysian banks are competing heavily for fixed deposits. How will this hit sector margins?", "Banking Sector", "Financial Services", "price_drop", "sector", "balanced", true, 40 },
            { "sector-cpo-plantation", "Sector", "Plantation sector · CPO export duty & production volume", "Crude Palm Oil price swings influenced by regional weather patterns and biodiesel mandates.", "CPO prices crossed RM4,200 per tonne. Are plantation stocks entering an upcycle?", "Plantation Sector", "Plantations", "opportunity", "sector", "opportunity", true, 50 },
            { "market-bnm-opr", "MalaysianMarket", "Bank Negara Malaysia · Overnight Policy Rate change", "Domestic benchmark interest rate shift affecting loan repayments, savings, and consumer spending.", "BNM is expected to adjust the OPR rate. Which Malaysian sectors are most sensitive?", "Bursa Malaysia (KLCI)", "Broad Market", "macro_shock", "malaysian_market", "capital_preservation", true, 60 },
            { "market-myr-fx", "MalaysianMarket", "Ringgit movement · Import vs export dynamics", "Currency appreciation or depreciation shifting costs for importers and gains for exporters.", "The Ringgit strengthened against the USD. Does this hurt export stocks or help domestic retail?", "MYR Foreign Exchange", "Multi-Sector", "macro_shock", "malaysian_market", "balanced", true, 70 },
            { "global-us-fed-rate", "GlobalEconomy", "US Federal Reserve · Interest rate decision & USD liquidity", "Global monetary tightening or easing shifting foreign fund flows in emerging markets.", "US Fed increases interest rates by 0.25 points. What could happen to Maybank and Malaysian equities?", "Maybank / KLCI", "Financials & Equities", "macro_shock", "global_economy", "capital_preservation", true, 80 },
            { "global-tariffs-trade", "GlobalEconomy", "Global trade policy & tariffs · Supply chain reallocation", "Cross-border tariff announcements affecting multinational manufacturers and electronics exporters.", "New US trade tariffs announced on Asian electronics. How will supply chains adjust?", "Tech & Manufacturing", "Technology & Industrials", "macro_shock", "global_economy", "capital_preservation", true, 90 },
            { "portfolio-concentration-risk", "PortfolioDecision", "Portfolio concentration · Single security over-allocation", "Holding more than 30% of total portfolio in one company increases vulnerability to single shocks.", "My largest holding now makes up 40% of my total portfolio. Should I rebalance to reduce risk?", "Member Portfolio", "Diversified", "concentration", "portfolio", "capital_preservation", true, 100 },
            { "portfolio-cash-drag", "PortfolioDecision", "Cash drag vs reinvestment · Liquidity deployment timing", "High cash reserves sitting uninvested while inflation erodes purchasing power.", "I have 35% cash in my investment account waiting for a crash. How should I pace deployment?", "Cash & Equities", "Cash & Multi-Asset", "opportunity", "portfolio", "balanced", true, 110 },
            { "opp-dividend-yield-window", "Opportunity", "Dividend stalwart pullback · Yield entry", "High-quality dividend payer trading at an elevated trailing dividend yield following market weakness.", "Quality banking and utility stocks dropped this month, pushing dividend yields higher. Is it worth researching them?", "Dividend Champions", "Financials & Utilities", "opportunity", "sector", "income", true, 120 },
            { "opp-oversold-reaction", "Opportunity", "Market overreaction · Sentiment vs cash flow", "A company meets financial guidance but suffers price declines due to broad market panic.", "A strong balance-sheet company sold off with the broader market. Is this a buying opportunity?", "Selected Quality Stocks", "Broad Market", "opportunity", "sector", "opportunity", true, 130 }
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DecisionSignalTemplate",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    SampleQuery = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    DefaultSecurity = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultSector = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultConcern = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DefaultScope = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DefaultObjective = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionSignalTemplate", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionSignalTemplate_IsActive_SortOrder",
                table: "DecisionSignalTemplate",
                columns: SignalTemplateIndexColumns);

            migrationBuilder.InsertData(
                table: "DecisionSignalTemplate",
                columns: SignalTemplateSeedColumns,
                values: SignalTemplateSeedValues);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionSignalTemplate");
        }
    }
}
