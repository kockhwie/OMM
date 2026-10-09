using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionSignalTemplateAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "DecisionSignalTemplate",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "DecisionSignalTemplate",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "DecisionSignalTemplate",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedByUserId",
                table: "DecisionSignalTemplate",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "DecisionSignalTemplate",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModifiedAt",
                table: "DecisionSignalTemplate",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedByUserId",
                table: "DecisionSignalTemplate",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DecisionSignalTemplate");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "DecisionSignalTemplate");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "DecisionSignalTemplate");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "DecisionSignalTemplate");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "DecisionSignalTemplate");

            migrationBuilder.DropColumn(
                name: "ModifiedAt",
                table: "DecisionSignalTemplate");

            migrationBuilder.DropColumn(
                name: "ModifiedByUserId",
                table: "DecisionSignalTemplate");
        }
    }
}
