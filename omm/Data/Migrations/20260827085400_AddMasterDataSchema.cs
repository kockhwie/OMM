using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace omm.Migrations
{
    /// <inheritdoc />
    public partial class AddMasterDataSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Country",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: false),
                    CountryName_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CountryName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CountryName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultCurrencyCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Country", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Country_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Country_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Country_AspNetUsers_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Exchange",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    ExchangeCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeName_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exchange", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exchange_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Exchange_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Exchange_AspNetUsers_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Exchange_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Institution",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryId = table.Column<int>(type: "int", nullable: true),
                    InstitutionCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstitutionName_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstitutionName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstitutionName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InstitutionCategory = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Institution", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Institution_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Institution_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Institution_AspNetUsers_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Institution_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Sector",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryId = table.Column<int>(type: "int", nullable: false),
                    SectorCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectorName_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectorName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SectorName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sector", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sector_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sector_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sector_AspNetUsers_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Sector_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Market",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExchangeId = table.Column<int>(type: "int", nullable: false),
                    MarketCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketName_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MarketName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Market", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Market_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Market_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Market_AspNetUsers_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Market_Exchange_ExchangeId",
                        column: x => x.ExchangeId,
                        principalTable: "Exchange",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubSector",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SectorId = table.Column<int>(type: "int", nullable: false),
                    SubSectorCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubSectorName_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubSectorName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SubSectorName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubSector", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubSector_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubSector_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubSector_AspNetUsers_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SubSector_Sector_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sector",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Stock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StockCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShortName_EN = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShortName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShortName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegalName_EN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegalName_ZH_TW = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LegalName_ZH_CN = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RicCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    YahooSymbol = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsinCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MarketId = table.Column<int>(type: "int", nullable: false),
                    SectorId = table.Column<int>(type: "int", nullable: true),
                    SubSectorId = table.Column<int>(type: "int", nullable: true),
                    ShariahCompliant = table.Column<bool>(type: "bit", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    MarketCap = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    EPS = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DPS = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    NTA = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ROE = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ROA = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DebtToEquity = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CurrentRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LastScrapedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PE = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PB = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DividendYield = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    LastCalculatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ModifiedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stock_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stock_AspNetUsers_DeletedByUserId",
                        column: x => x.DeletedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stock_AspNetUsers_ModifiedByUserId",
                        column: x => x.ModifiedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stock_Market_MarketId",
                        column: x => x.MarketId,
                        principalTable: "Market",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Stock_Sector_SectorId",
                        column: x => x.SectorId,
                        principalTable: "Sector",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stock_SubSector_SubSectorId",
                        column: x => x.SubSectorId,
                        principalTable: "SubSector",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "Country",
                columns: new[] { "Id", "CountryCode", "CountryName_EN", "CountryName_ZH_CN", "CountryName_ZH_TW", "CreatedAt", "CreatedByUserId", "DefaultCurrencyCode", "DeletedAt", "DeletedByUserId", "IsActive", "IsDeleted", "ModifiedAt", "ModifiedByUserId" },
                values: new object[] { 1, "MY", "Malaysia", "Malaysia", "Malaysia", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "MYR", null, null, true, false, null, null });

            migrationBuilder.InsertData(
                table: "Exchange",
                columns: new[] { "Id", "CountryId", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "ExchangeCode", "ExchangeName_EN", "ExchangeName_ZH_CN", "ExchangeName_ZH_TW", "IsActive", "IsDeleted", "ModifiedAt", "ModifiedByUserId" },
                values: new object[] { 1, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, "BURSA", "Bursa Malaysia", "Bursa Malaysia", "Bursa Malaysia", true, false, null, null });

            migrationBuilder.InsertData(
                table: "Institution",
                columns: new[] { "Id", "CountryId", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "InstitutionCategory", "InstitutionCode", "InstitutionName_EN", "InstitutionName_ZH_CN", "InstitutionName_ZH_TW", "IsActive", "IsDeleted", "ModifiedAt", "ModifiedByUserId" },
                values: new object[,]
                {
                    { 1, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 0, "MAYBANK", "Maybank", "Maybank", "Maybank", true, false, null, null },
                    { 2, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 0, "CIMB", "CIMB", "CIMB", "CIMB", true, false, null, null },
                    { 3, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 0, "PUBLIC-BANK", "Public Bank", "Public Bank", "Public Bank", true, false, null, null },
                    { 4, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 2, "KWSP", "KWSP", "KWSP", "KWSP", true, false, null, null },
                    { 5, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 5, "BURSA", "Bursa Malaysia", "Bursa Malaysia", "Bursa Malaysia", true, false, null, null }
                });

            migrationBuilder.InsertData(
                table: "Sector",
                columns: new[] { "Id", "CountryId", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "IsActive", "IsDeleted", "ModifiedAt", "ModifiedByUserId", "SectorCode", "SectorName_EN", "SectorName_ZH_CN", "SectorName_ZH_TW" },
                values: new object[,]
                {
                    { 1, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "FIN-SVC", "Financial Services", "Financial Services", "Financial Services" },
                    { 2, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "CONSUMER", "Consumer Products & Services", "Consumer Products & Services", "Consumer Products & Services" },
                    { 3, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "INDUSTRIAL", "Industrial Products & Services", "Industrial Products & Services", "Industrial Products & Services" },
                    { 4, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "TECH", "Technology", "Technology", "Technology" },
                    { 5, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "TEL-MEDIA", "Telecommunications & Media", "Telecommunications & Media", "Telecommunications & Media" },
                    { 6, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "HEALTH", "Health Care", "Health Care", "Health Care" },
                    { 7, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "PROPERTY", "Property", "Property", "Property" },
                    { 8, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "REIT", "Real Estate Investment Trusts (REITs)", "Real Estate Investment Trusts (REITs)", "Real Estate Investment Trusts (REITs)" },
                    { 9, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "PLANTATION", "Plantation", "Plantation", "Plantation" },
                    { 10, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "ENERGY", "Energy", "Energy", "Energy" },
                    { 11, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "CONSTRUCTION", "Construction", "Construction", "Construction" },
                    { 12, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "TRANSPORT", "Transportation & Logistics", "Transportation & Logistics", "Transportation & Logistics" },
                    { 13, 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, "UTILITIES", "Utilities", "Utilities", "Utilities" }
                });

            migrationBuilder.InsertData(
                table: "Market",
                columns: new[] { "Id", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "ExchangeId", "IsActive", "IsDeleted", "MarketCode", "MarketName_EN", "MarketName_ZH_CN", "MarketName_ZH_TW", "ModifiedAt", "ModifiedByUserId" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 1, true, false, "MAIN", "Main Market", "Main Market", "Main Market", null, null },
                    { 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 1, true, false, "ACE", "ACE Market", "ACE Market", "ACE Market", null, null },
                    { 3, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, 1, true, false, "LEAP", "LEAP Market", "LEAP Market", "LEAP Market", null, null }
                });

            migrationBuilder.InsertData(
                table: "SubSector",
                columns: new[] { "Id", "CreatedAt", "CreatedByUserId", "DeletedAt", "DeletedByUserId", "IsActive", "IsDeleted", "ModifiedAt", "ModifiedByUserId", "SectorId", "SubSectorCode", "SubSectorName_EN", "SubSectorName_ZH_CN", "SubSectorName_ZH_TW" },
                values: new object[,]
                {
                    { 1, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 1, "BANKING", "Banking", "Banking", "Banking" },
                    { 2, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 1, "INSURANCE", "Insurance", "Insurance", "Insurance" },
                    { 3, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 1, "OTHER-FIN", "Other Financial Services", "Other Financial Services", "Other Financial Services" },
                    { 4, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 2, "FOOD-BEV", "Food & Beverages", "Food & Beverages", "Food & Beverages" },
                    { 5, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 2, "RETAILERS", "Retailers", "Retailers", "Retailers" },
                    { 6, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 2, "AUTOMOTIVE", "Automotive", "Automotive", "Automotive" },
                    { 7, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 2, "CONSUMER-SVC", "Consumer Services", "Consumer Services", "Consumer Services" },
                    { 8, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 2, "HOUSEHOLD", "Household Goods", "Household Goods", "Household Goods" },
                    { 9, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 2, "AGRI", "Agricultural Products", "Agricultural Products", "Agricultural Products" },
                    { 10, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 2, "TRAVEL", "Travel Leisure & Hospitality", "Travel Leisure & Hospitality", "Travel Leisure & Hospitality" },
                    { 11, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 3, "BUILDING", "Building Materials", "Building Materials", "Building Materials" },
                    { 12, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 3, "CHEMICALS", "Chemicals", "Chemicals", "Chemicals" },
                    { 13, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 3, "METALS", "Metals", "Metals", "Metals" },
                    { 14, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 3, "PACKAGING", "Packaging Materials", "Packaging Materials", "Packaging Materials" },
                    { 15, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 3, "DIVERSIFIED", "Diversified Industrials", "Diversified Industrials", "Diversified Industrials" },
                    { 16, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 3, "IND-ENGINEERING", "Industrial Engineering", "Industrial Engineering", "Industrial Engineering" },
                    { 17, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 4, "SEMICONDUCTORS", "Semiconductors", "Semiconductors", "Semiconductors" },
                    { 18, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 4, "SOFTWARE", "Software", "Software", "Software" },
                    { 19, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 4, "DIGITAL", "Digital Services", "Digital Services", "Digital Services" },
                    { 20, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 4, "HARDWARE", "Hardware", "Hardware", "Hardware" },
                    { 21, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 5, "TELCO-SVC", "Telecommunications Service Providers", "Telecommunications Service Providers", "Telecommunications Service Providers" },
                    { 22, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 5, "MEDIA", "Media & Advertising", "Media & Advertising", "Media & Advertising" },
                    { 23, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 5, "TELCO-EQUIP", "Telecommunications Equipment", "Telecommunications Equipment", "Telecommunications Equipment" },
                    { 24, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 6, "HEALTHCARE", "Healthcare Providers", "Healthcare Providers", "Healthcare Providers" },
                    { 25, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 6, "PHARMA", "Pharmaceuticals", "Pharmaceuticals", "Pharmaceuticals" },
                    { 26, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 6, "HEALTH-EQUIP", "Healthcare Equipment & Supplies", "Healthcare Equipment & Supplies", "Healthcare Equipment & Supplies" },
                    { 27, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 7, "PROPERTY-DEV", "Property Development", "Property Development", "Property Development" },
                    { 28, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 7, "PROPERTY-INV", "Property Investment & Management", "Property Investment & Management", "Property Investment & Management" },
                    { 29, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 8, "COMMERCIAL", "Commercial", "Commercial", "Commercial" },
                    { 30, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 8, "RETAIL", "Retail", "Retail", "Retail" },
                    { 31, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 8, "INDUSTRIAL", "Industrial", "Industrial", "Industrial" },
                    { 32, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 8, "HOSPITALITY", "Hospitality", "Hospitality", "Hospitality" },
                    { 33, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 8, "HEALTHCARE-REIT", "Healthcare", "Healthcare", "Healthcare" },
                    { 34, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 9, "UPSTREAM", "Upstream Plantation", "Upstream Plantation", "Upstream Plantation" },
                    { 35, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 9, "INTEGRATED", "Integrated Cultivation", "Integrated Cultivation", "Integrated Cultivation" },
                    { 36, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 10, "OIL-GAS-PROD", "Oil & Gas Producers", "Oil & Gas Producers", "Oil & Gas Producers" },
                    { 37, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 10, "OIL-GAS-EQUIP", "Oil & Gas Equipment & Services", "Oil & Gas Equipment & Services", "Oil & Gas Equipment & Services" },
                    { 38, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 10, "RENEWABLE", "Renewable Energy", "Renewable Energy", "Renewable Energy" },
                    { 39, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 11, "CIVIL", "Civil Engineering", "Civil Engineering", "Civil Engineering" },
                    { 40, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 11, "HEAVY", "Heavy Construction", "Heavy Construction", "Heavy Construction" },
                    { 41, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 11, "SPECIALISED", "Specialised Construction", "Specialised Construction", "Specialised Construction" },
                    { 42, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 12, "LOGISTICS", "Logistics Services", "Logistics Services", "Logistics Services" },
                    { 43, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 12, "PORTS", "Ports & Shipping", "Ports & Shipping", "Ports & Shipping" },
                    { 44, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 12, "AIRLINES", "Airlines & Aviation", "Airlines & Aviation", "Airlines & Aviation" },
                    { 45, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 12, "ROAD-RAIL", "Road & Rail", "Road & Rail", "Road & Rail" },
                    { 46, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 13, "ELECTRICITY", "Electricity", "Electricity", "Electricity" },
                    { 47, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, null, true, false, null, null, 13, "GAS-WATER", "Gas & Water Distribution", "Gas & Water Distribution", "Gas & Water Distribution" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Country_CreatedByUserId",
                table: "Country",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Country_DeletedByUserId",
                table: "Country",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Country_ModifiedByUserId",
                table: "Country",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Exchange_CountryId",
                table: "Exchange",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Exchange_CreatedByUserId",
                table: "Exchange",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Exchange_DeletedByUserId",
                table: "Exchange",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Exchange_ModifiedByUserId",
                table: "Exchange",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Institution_CountryId",
                table: "Institution",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Institution_CreatedByUserId",
                table: "Institution",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Institution_DeletedByUserId",
                table: "Institution",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Institution_ModifiedByUserId",
                table: "Institution",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Market_CreatedByUserId",
                table: "Market",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Market_DeletedByUserId",
                table: "Market",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Market_ExchangeId",
                table: "Market",
                column: "ExchangeId");

            migrationBuilder.CreateIndex(
                name: "IX_Market_ModifiedByUserId",
                table: "Market",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sector_CountryId",
                table: "Sector",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Sector_CreatedByUserId",
                table: "Sector",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sector_DeletedByUserId",
                table: "Sector",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Sector_ModifiedByUserId",
                table: "Sector",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_CreatedByUserId",
                table: "Stock",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_DeletedByUserId",
                table: "Stock",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_MarketId",
                table: "Stock",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_ModifiedByUserId",
                table: "Stock",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_SectorId",
                table: "Stock",
                column: "SectorId");

            migrationBuilder.CreateIndex(
                name: "IX_Stock_SubSectorId",
                table: "Stock",
                column: "SubSectorId");

            migrationBuilder.CreateIndex(
                name: "IX_SubSector_CreatedByUserId",
                table: "SubSector",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubSector_DeletedByUserId",
                table: "SubSector",
                column: "DeletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubSector_ModifiedByUserId",
                table: "SubSector",
                column: "ModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubSector_SectorId",
                table: "SubSector",
                column: "SectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Institution");

            migrationBuilder.DropTable(
                name: "Stock");

            migrationBuilder.DropTable(
                name: "Market");

            migrationBuilder.DropTable(
                name: "SubSector");

            migrationBuilder.DropTable(
                name: "Exchange");

            migrationBuilder.DropTable(
                name: "Sector");

            migrationBuilder.DropTable(
                name: "Country");
        }
    }
}
