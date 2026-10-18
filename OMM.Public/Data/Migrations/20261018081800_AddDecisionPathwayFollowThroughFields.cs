using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261018081800_AddDecisionPathwayFollowThroughFields")]
    public partial class AddDecisionPathwayFollowThroughFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ReviewDate",
                table: "DecisionPathwayRecord",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2026, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "TriggerPriceBelow",
                table: "DecisionPathwayRecord",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TriggerPriceAbove",
                table: "DecisionPathwayRecord",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DecisionPathwayRecord",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Open");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "DecisionPathwayRecord",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StockCode",
                table: "DecisionPathwayRecord",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DecisionPathwayRecord_UserId_Status_ReviewDate",
                table: "DecisionPathwayRecord",
                columns: new[] { "UserId", "Status", "ReviewDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_DecisionPathwayRecord_UserId_Status_ReviewDate\";");

            migrationBuilder.DropColumn(
                name: "ReviewDate",
                table: "DecisionPathwayRecord");

            migrationBuilder.DropColumn(
                name: "TriggerPriceBelow",
                table: "DecisionPathwayRecord");

            migrationBuilder.DropColumn(
                name: "TriggerPriceAbove",
                table: "DecisionPathwayRecord");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DecisionPathwayRecord");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "DecisionPathwayRecord");

            migrationBuilder.DropColumn(
                name: "StockCode",
                table: "DecisionPathwayRecord");
        }
    }
}
