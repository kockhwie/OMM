using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMineYieldHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MineYieldHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MineId = table.Column<Guid>(type: "uuid", nullable: false),
                    EffectiveYear = table.Column<int>(type: "integer", nullable: false),
                    RecordDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RatePct = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    DeclaredAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BalanceAtTime = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MineYieldHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MineYieldHistory_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MineYieldHistory_Mine_MineId",
                        column: x => x.MineId,
                        principalTable: "Mine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MineYieldHistory_MineId_EffectiveYear",
                table: "MineYieldHistory",
                columns: new[] { "MineId", "EffectiveYear" });

            migrationBuilder.CreateIndex(
                name: "IX_MineYieldHistory_UserId_Id",
                table: "MineYieldHistory",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MineYieldHistory_UserId_IsDeleted",
                table: "MineYieldHistory",
                columns: new[] { "UserId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MineYieldHistory");
        }
    }
}
