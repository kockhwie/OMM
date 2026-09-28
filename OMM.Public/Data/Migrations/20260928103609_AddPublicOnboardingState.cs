using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicOnboardingState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardingCompletedAt",
                table: "MinerProfile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardingSkippedAt",
                table: "MinerProfile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OnboardingStartedAt",
                table: "MinerProfile",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OnboardingStatus",
                table: "MinerProfile",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotStarted");

            migrationBuilder.AddColumn<string>(
                name: "OnboardingStep",
                table: "MinerProfile",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Welcome");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnboardingCompletedAt",
                table: "MinerProfile");

            migrationBuilder.DropColumn(
                name: "OnboardingSkippedAt",
                table: "MinerProfile");

            migrationBuilder.DropColumn(
                name: "OnboardingStartedAt",
                table: "MinerProfile");

            migrationBuilder.DropColumn(
                name: "OnboardingStatus",
                table: "MinerProfile");

            migrationBuilder.DropColumn(
                name: "OnboardingStep",
                table: "MinerProfile");
        }
    }
}
