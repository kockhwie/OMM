using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionPathwayRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DecisionPathwayRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SignalId = table.Column<string>(type: "text", nullable: false),
                    SignalTitle = table.Column<string>(type: "text", nullable: false),
                    ConcernKey = table.Column<string>(type: "text", nullable: false),
                    ScopeKey = table.Column<string>(type: "text", nullable: false),
                    ObjectiveKey = table.Column<string>(type: "text", nullable: false),
                    SelectedActionKey = table.Column<string>(type: "text", nullable: false),
                    CurrentSharePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AnnualDividendPerShare = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    HorizonLabel = table.Column<string>(type: "text", nullable: false),
                    CheckpointTitle = table.Column<string>(type: "text", nullable: false),
                    CheckpointTriggerCondition = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionPathwayRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionPathwayRecord_UserId_CreatedAt",
                table: "DecisionPathwayRecord",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionPathwayRecord");
        }
    }
}
