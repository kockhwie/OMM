using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedPhase5InquiryPurposes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "public",
                table: "InquiryPurpose",
                columns: new[] { "Code", "DescriptionEn", "DescriptionZh", "DisplayNameEn", "DisplayNameZh", "IsActive", "SortOrder" },
                values: new object[,]
                {
                    { "AFFECTED_ASSETS", "Identify sectors or securities directly connected to the topic.", null, "Affected sectors or stocks", null, true, 4 },
                    { "COMPARE_SCENARIOS", "Compare outcomes under different assumptions.", null, "Compare scenarios", null, true, 6 },
                    { "CRYSTALBALL_PLAN", "Continue through a structured planning flow.", null, "Continue with CrystalBall planning", null, true, 8 },
                    { "EXPLAIN", "Understand the event, claim, or market development.", null, "Explain what happened", null, true, 1 },
                    { "GUIDE_ME", "Let OMM guide you through the most relevant next questions.", null, "I’m not sure — guide me", null, true, 9 },
                    { "MALAYSIA_IMPACT", "Explore possible effects on Malaysia and its markets.", null, "Impact on Malaysia", null, true, 2 },
                    { "PORTFOLIO_IMPACT", "Relate the topic to your recorded portfolio facts.", null, "Impact on my portfolio", null, true, 3 },
                    { "RISKS_OPPORTUNITIES", "Review potential benefits, risks, and uncertainties.", null, "Risks and opportunities", null, true, 5 },
                    { "SET_ALERT", "Choose a condition to monitor. Alerts require separate confirmation.", null, "Set an alert", null, true, 7 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "AFFECTED_ASSETS");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "COMPARE_SCENARIOS");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "CRYSTALBALL_PLAN");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "EXPLAIN");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "GUIDE_ME");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "MALAYSIA_IMPACT");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "PORTFOLIO_IMPACT");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "RISKS_OPPORTUNITIES");

            migrationBuilder.DeleteData(
                schema: "public",
                table: "InquiryPurpose",
                keyColumn: "Code",
                keyValue: "SET_ALERT");
        }
    }
}
