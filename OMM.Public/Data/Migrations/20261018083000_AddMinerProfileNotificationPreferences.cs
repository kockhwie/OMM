using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261018083000_AddMinerProfileNotificationPreferences")]
    public partial class AddMinerProfileNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyFdMaturity",
                table: "MinerProfile",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyGoalMilestones",
                table: "MinerProfile",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDataFreshness",
                table: "MinerProfile",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDecisionReview",
                table: "MinerProfile",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyFdMaturity",
                table: "MinerProfile");

            migrationBuilder.DropColumn(
                name: "NotifyGoalMilestones",
                table: "MinerProfile");

            migrationBuilder.DropColumn(
                name: "NotifyDataFreshness",
                table: "MinerProfile");

            migrationBuilder.DropColumn(
                name: "NotifyDecisionReview",
                table: "MinerProfile");
        }
    }
}
