using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGoalCurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrencyId",
                table: "Goal",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Goal_CurrencyId",
                table: "Goal",
                column: "CurrencyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Goal_Currency_CurrencyId",
                table: "Goal",
                column: "CurrencyId",
                principalTable: "Currency",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goal_Currency_CurrencyId",
                table: "Goal");

            migrationBuilder.DropIndex(
                name: "IX_Goal_CurrencyId",
                table: "Goal");

            migrationBuilder.DropColumn(
                name: "CurrencyId",
                table: "Goal");
        }
    }
}
