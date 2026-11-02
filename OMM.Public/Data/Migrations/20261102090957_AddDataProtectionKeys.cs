using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataProtectionKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InquiryEntityLink",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InquirySessionId = table.Column<long>(type: "bigint", nullable: false),
                    InquiryTurnId = table.Column<long>(type: "bigint", nullable: true),
                    EntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ResolutionMethod = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: true),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    UserConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquiryEntityLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InquiryEntityLink_InquirySession_InquirySessionId",
                        column: x => x.InquirySessionId,
                        principalSchema: "public",
                        principalTable: "InquirySession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InquiryEntityLink_InquiryTurn_InquiryTurnId",
                        column: x => x.InquiryTurnId,
                        principalSchema: "public",
                        principalTable: "InquiryTurn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InquiryOutcome",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InquirySessionId = table.Column<long>(type: "bigint", nullable: false),
                    FinalRoute = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    ClarificationCount = table.Column<int>(type: "integer", nullable: false),
                    AbandonmentPoint = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CompletionStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AlertCreated = table.Column<bool>(type: "boolean", nullable: false),
                    SavedAnalysis = table.Column<bool>(type: "boolean", nullable: false),
                    PathwayCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquiryOutcome", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InquiryOutcome_InquirySession_InquirySessionId",
                        column: x => x.InquirySessionId,
                        principalSchema: "public",
                        principalTable: "InquirySession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InquirySelectionEvent",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InquirySessionId = table.Column<long>(type: "bigint", nullable: false),
                    InquiryTurnId = table.Column<long>(type: "bigint", nullable: true),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PurposeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SelectionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquirySelectionEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InquirySelectionEvent_InquirySession_InquirySessionId",
                        column: x => x.InquirySessionId,
                        principalSchema: "public",
                        principalTable: "InquirySession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InquirySelectionEvent_InquiryTurn_InquiryTurnId",
                        column: x => x.InquiryTurnId,
                        principalSchema: "public",
                        principalTable: "InquiryTurn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InquiryTelemetryEvent",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InquirySessionId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquiryTelemetryEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InquiryTelemetryEvent_InquirySession_InquirySessionId",
                        column: x => x.InquirySessionId,
                        principalSchema: "public",
                        principalTable: "InquirySession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Phase5EventTypeRule",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase5EventTypeRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Phase5ImpactRule",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventTypeRuleId = table.Column<int>(type: "integer", nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SourceEntityKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TargetEntityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    TargetEntityKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TransmissionMechanism = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Strength = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeHorizon = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Assumptions = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    MalaysiaRelevant = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phase5ImpactRule", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Phase5ImpactRule_Phase5EventTypeRule_EventTypeRuleId",
                        column: x => x.EventTypeRuleId,
                        principalSchema: "public",
                        principalTable: "Phase5EventTypeRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "Phase5EventTypeRule",
                columns: new[] { "Id", "Code", "CreatedAt", "DisplayName", "EffectiveFrom", "EffectiveTo", "IsActive", "IsSensitive", "Version" },
                values: new object[,]
                {
                    { 1, "RATE_CHANGE", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Interest-rate change", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, 1 },
                    { 2, "COMMODITY_MOVE", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Commodity price movement", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, false, 1 },
                    { 3, "GOVERNANCE_EVENT", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Governance or regulatory event", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, true, true, 1 }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "Phase5ImpactRule",
                columns: new[] { "Id", "Assumptions", "CreatedAt", "CreatedByUserId", "Direction", "EffectiveFrom", "EffectiveTo", "EventTypeRuleId", "IsActive", "MalaysiaRelevant", "SourceEntityKey", "SourceEntityType", "Strength", "TargetEntityKey", "TargetEntityType", "TimeHorizon", "TransmissionMechanism", "Version" },
                values: new object[,]
                {
                    { 1L, null, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", "Mixed", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 1, true, true, "POLICY_RATE", "Rate", "Moderate", "BANKING", "Sector", "MediumTerm", "Funding cost and credit demand", 1 },
                    { 2L, null, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", "Positive", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 2, true, true, "CRUDE_PALM_OIL", "Commodity", "Moderate", "PLANTATION", "Sector", "ShortTerm", "Export revenue and margin", 1 },
                    { 3L, null, new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "system", "Negative", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, 3, true, true, "REGULATORY_ACTION", "Event", "Material", "EXPOSURE_REVIEW", "Portfolio", "ShortTerm", "Uncertainty and valuation sensitivity", 1 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_InquiryEntityLink_InquirySessionId_EntityType_EntityKey",
                schema: "public",
                table: "InquiryEntityLink",
                columns: new[] { "InquirySessionId", "EntityType", "EntityKey" });

            migrationBuilder.CreateIndex(
                name: "IX_InquiryEntityLink_InquiryTurnId",
                schema: "public",
                table: "InquiryEntityLink",
                column: "InquiryTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_InquiryOutcome_InquirySessionId",
                schema: "public",
                table: "InquiryOutcome",
                column: "InquirySessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InquiryOutcome_UpdatedAt",
                schema: "public",
                table: "InquiryOutcome",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InquirySelectionEvent_InquirySessionId_OccurredAt",
                schema: "public",
                table: "InquirySelectionEvent",
                columns: new[] { "InquirySessionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InquirySelectionEvent_InquiryTurnId",
                schema: "public",
                table: "InquirySelectionEvent",
                column: "InquiryTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_InquiryTelemetryEvent_EventType_OccurredAt",
                schema: "public",
                table: "InquiryTelemetryEvent",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InquiryTelemetryEvent_ExpiresAt",
                schema: "public",
                table: "InquiryTelemetryEvent",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_InquiryTelemetryEvent_InquirySessionId",
                schema: "public",
                table: "InquiryTelemetryEvent",
                column: "InquirySessionId");

            migrationBuilder.CreateIndex(
                name: "IX_Phase5EventTypeRule_Code_Version",
                schema: "public",
                table: "Phase5EventTypeRule",
                columns: new[] { "Code", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Phase5EventTypeRule_IsActive_EffectiveFrom_EffectiveTo",
                schema: "public",
                table: "Phase5EventTypeRule",
                columns: new[] { "IsActive", "EffectiveFrom", "EffectiveTo" });

            migrationBuilder.CreateIndex(
                name: "IX_Phase5ImpactRule_EventTypeRuleId_SourceEntityType_SourceEnt~",
                schema: "public",
                table: "Phase5ImpactRule",
                columns: new[] { "EventTypeRuleId", "SourceEntityType", "SourceEntityKey", "TargetEntityType", "TargetEntityKey", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Phase5ImpactRule_IsActive_EffectiveFrom_EffectiveTo",
                schema: "public",
                table: "Phase5ImpactRule",
                columns: new[] { "IsActive", "EffectiveFrom", "EffectiveTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "InquiryEntityLink",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InquiryOutcome",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InquirySelectionEvent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InquiryTelemetryEvent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Phase5ImpactRule",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Phase5EventTypeRule",
                schema: "public");
        }
    }
}
