using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPublicMemberData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Currency",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Name_EN = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Goal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Current = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Goal", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Goal_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DismissedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DedupeKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notification", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notification_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Expense",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expense", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expense_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expense_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MinerProfile",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CountryId = table.Column<int>(type: "integer", nullable: true),
                    CurrencyId = table.Column<int>(type: "integer", nullable: true),
                    Language = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinerProfile", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MinerProfile_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinerProfile_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MinerProfile_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Burden",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MonthlyPayment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    LinkedMineId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Burden", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Burden_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Burden_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Mine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    InstitutionId = table.Column<int>(type: "integer", nullable: true),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    CurrentValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PurchaseCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Growth = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GrowthPct = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    MonthlyIncome = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Holdings = table.Column<string>(type: "text", nullable: true),
                    LinkedBurdenId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UpdatedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mine", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Mine_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Mine_Burden_LinkedBurdenId",
                        column: x => x.LinkedBurdenId,
                        principalTable: "Burden",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Mine_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Mine_Institution_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "Institution",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GoalMine",
                columns: table => new
                {
                    GoalId = table.Column<Guid>(type: "uuid", nullable: false),
                    MineId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GoalMine", x => new { x.GoalId, x.MineId });
                    table.ForeignKey(
                        name: "FK_GoalMine_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalMine_Goal_GoalId",
                        column: x => x.GoalId,
                        principalTable: "Goal",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GoalMine_Mine_MineId",
                        column: x => x.MineId,
                        principalTable: "Mine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IncomeRecord",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrencyId = table.Column<int>(type: "integer", nullable: false),
                    Frequency = table.Column<int>(type: "integer", nullable: false),
                    MineId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordDate = table.Column<DateOnly>(type: "date", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncomeRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncomeRecord_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IncomeRecord_Currency_CurrencyId",
                        column: x => x.CurrencyId,
                        principalTable: "Currency",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IncomeRecord_Mine_MineId",
                        column: x => x.MineId,
                        principalTable: "Mine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MinePosition",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    PositionType = table.Column<int>(type: "integer", nullable: false),
                    Principal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Rate = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    MaturityDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Quantity = table.Column<string>(type: "text", nullable: true),
                    PurchasePrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    StockId = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MinePosition", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MinePosition_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinePosition_Mine_MineId",
                        column: x => x.MineId,
                        principalTable: "Mine",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MinePosition_Stock_StockId",
                        column: x => x.StockId,
                        principalTable: "Stock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.InsertData(
                table: "Currency",
                columns: new[] { "Id", "Code", "CreatedAt", "IsActive", "IsDeleted", "Name_EN" },
                values: new object[,]
                {
                    { 1, "MYR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, "Malaysian Ringgit" },
                    { 2, "SGD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, "Singapore Dollar" },
                    { 3, "USD", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, "US Dollar" },
                    { 4, "EUR", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, false, "Euro" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Burden_CurrencyId",
                table: "Burden",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Burden_LinkedMineId",
                table: "Burden",
                column: "LinkedMineId");

            migrationBuilder.CreateIndex(
                name: "IX_Burden_MaturityDate",
                table: "Burden",
                column: "MaturityDate");

            migrationBuilder.CreateIndex(
                name: "IX_Burden_UserId_Id",
                table: "Burden",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Burden_UserId_IsDeleted",
                table: "Burden",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Currency_Code",
                table: "Currency",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expense_CurrencyId",
                table: "Expense",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Expense_UserId_Id",
                table: "Expense",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expense_UserId_IsDeleted",
                table: "Expense",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Goal_UserId_Id",
                table: "Goal",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Goal_UserId_IsDeleted",
                table: "Goal",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_GoalMine_MineId",
                table: "GoalMine",
                column: "MineId");

            migrationBuilder.CreateIndex(
                name: "IX_GoalMine_UserId_IsDeleted",
                table: "GoalMine",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_IncomeRecord_CurrencyId",
                table: "IncomeRecord",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeRecord_MineId",
                table: "IncomeRecord",
                column: "MineId");

            migrationBuilder.CreateIndex(
                name: "IX_IncomeRecord_UserId_Id",
                table: "IncomeRecord",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IncomeRecord_UserId_IsDeleted",
                table: "IncomeRecord",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Mine_CurrencyId",
                table: "Mine",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Mine_InstitutionId",
                table: "Mine",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_Mine_LinkedBurdenId",
                table: "Mine",
                column: "LinkedBurdenId");

            migrationBuilder.CreateIndex(
                name: "IX_Mine_UserId_Id",
                table: "Mine",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Mine_UserId_IsDeleted",
                table: "Mine",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MinePosition_MaturityDate",
                table: "MinePosition",
                column: "MaturityDate");

            migrationBuilder.CreateIndex(
                name: "IX_MinePosition_MineId",
                table: "MinePosition",
                column: "MineId");

            migrationBuilder.CreateIndex(
                name: "IX_MinePosition_StockId",
                table: "MinePosition",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_MinePosition_UserId_Id",
                table: "MinePosition",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MinePosition_UserId_IsDeleted",
                table: "MinePosition",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_MinerProfile_CountryId",
                table: "MinerProfile",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_MinerProfile_CurrencyId",
                table: "MinerProfile",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId_DedupeKey",
                table: "Notification",
                columns: new[] { "UserId", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId_Id",
                table: "Notification",
                columns: new[] { "UserId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId_IsDeleted",
                table: "Notification",
                columns: new[] { "UserId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Notification_UserId_ReadAt_DismissedAt",
                table: "Notification",
                columns: new[] { "UserId", "ReadAt", "DismissedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_Burden_Mine_LinkedMineId",
                table: "Burden",
                column: "LinkedMineId",
                principalTable: "Mine",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Burden_Currency_CurrencyId",
                table: "Burden");

            migrationBuilder.DropForeignKey(
                name: "FK_Mine_Currency_CurrencyId",
                table: "Mine");

            migrationBuilder.DropForeignKey(
                name: "FK_Burden_Mine_LinkedMineId",
                table: "Burden");

            migrationBuilder.DropTable(
                name: "Expense");

            migrationBuilder.DropTable(
                name: "GoalMine");

            migrationBuilder.DropTable(
                name: "IncomeRecord");

            migrationBuilder.DropTable(
                name: "MinePosition");

            migrationBuilder.DropTable(
                name: "MinerProfile");

            migrationBuilder.DropTable(
                name: "Notification");

            migrationBuilder.DropTable(
                name: "Goal");

            migrationBuilder.DropTable(
                name: "Currency");

            migrationBuilder.DropTable(
                name: "Mine");

            migrationBuilder.DropTable(
                name: "Burden");
        }
    }
}
