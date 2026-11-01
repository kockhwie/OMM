using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OMM.Public.Data.Migrations;

[Migration("20261029133000_AddPhase5AnalyticsAndRules")]
public partial class AddPhase5AnalyticsAndRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "InquiryEntityLink", schema: "public",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
            }, constraints: table =>
            {
                table.PrimaryKey("PK_InquiryEntityLink", x => x.Id);
                table.ForeignKey("FK_InquiryEntityLink_InquirySession", x => x.InquirySessionId, "InquirySession", "Id", principalSchema: "public", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_InquiryEntityLink_InquiryTurn", x => x.InquiryTurnId, "InquiryTurn", "Id", principalSchema: "public", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "InquirySelectionEvent", schema: "public",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                InquirySessionId = table.Column<long>(type: "bigint", nullable: false),
                InquiryTurnId = table.Column<long>(type: "bigint", nullable: true),
                EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                PurposeCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                SelectionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_InquirySelectionEvent", x => x.Id);
                table.ForeignKey("FK_InquirySelectionEvent_InquirySession", x => x.InquirySessionId, "InquirySession", "Id", principalSchema: "public", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_InquirySelectionEvent_InquiryTurn", x => x.InquiryTurnId, "InquiryTurn", "Id", principalSchema: "public", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "InquiryOutcome", schema: "public",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
            }, constraints: table =>
            {
                table.PrimaryKey("PK_InquiryOutcome", x => x.Id);
                table.ForeignKey("FK_InquiryOutcome_InquirySession", x => x.InquirySessionId, "InquirySession", "Id", principalSchema: "public", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InquiryTelemetryEvent", schema: "public",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                InquirySessionId = table.Column<long>(type: "bigint", nullable: true),
                UserId = table.Column<string>(type: "text", nullable: true),
                EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("PK_InquiryTelemetryEvent", x => x.Id);
                table.ForeignKey("FK_InquiryTelemetryEvent_InquirySession", x => x.InquirySessionId, "InquirySession", "Id", principalSchema: "public", onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Phase5EventTypeRule", schema: "public",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<int>(type: "integer", nullable: false),
                EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EffectiveTo = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            }, constraints: table => table.PrimaryKey("PK_Phase5EventTypeRule", x => x.Id));

        migrationBuilder.CreateTable(
            name: "Phase5ImpactRule", schema: "public",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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
            }, constraints: table =>
            {
                table.PrimaryKey("PK_Phase5ImpactRule", x => x.Id);
                table.ForeignKey("FK_Phase5ImpactRule_Phase5EventTypeRule", x => x.EventTypeRuleId, "Phase5EventTypeRule", "Id", principalSchema: "public", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_InquiryEntityLink_Session_Entity", "InquiryEntityLink", new[] { "InquirySessionId", "EntityType", "EntityKey" }, schema: "public");
        migrationBuilder.CreateIndex("IX_InquirySelectionEvent_Session_OccurredAt", "InquirySelectionEvent", new[] { "InquirySessionId", "OccurredAt" }, schema: "public");
        migrationBuilder.CreateIndex("IX_InquiryOutcome_InquirySessionId", "InquiryOutcome", "InquirySessionId", unique: true, schema: "public");
        migrationBuilder.CreateIndex("IX_InquiryOutcome_UpdatedAt", "InquiryOutcome", "UpdatedAt", schema: "public");
        migrationBuilder.CreateIndex("IX_InquiryTelemetryEvent_Event_OccurredAt", "InquiryTelemetryEvent", new[] { "EventType", "OccurredAt" }, schema: "public");
        migrationBuilder.CreateIndex("IX_InquiryTelemetryEvent_ExpiresAt", "InquiryTelemetryEvent", "ExpiresAt", schema: "public");
        migrationBuilder.CreateIndex("IX_Phase5EventTypeRule_Code_Version", "Phase5EventTypeRule", new[] { "Code", "Version" }, unique: true, schema: "public");
        migrationBuilder.CreateIndex("IX_Phase5ImpactRule_Event_Source_Target_Version", "Phase5ImpactRule", new[] { "EventTypeRuleId", "SourceEntityType", "SourceEntityKey", "TargetEntityType", "TargetEntityKey", "Version" }, unique: true, schema: "public");
        migrationBuilder.InsertData("Phase5EventTypeRule", new[] { "Id", "Code", "DisplayName", "IsSensitive", "IsActive", "Version", "EffectiveFrom", "CreatedAt" }, new object[,] { { 1, "RATE_CHANGE", "Interest-rate change", false, true, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) }, { 2, "COMMODITY_MOVE", "Commodity price movement", false, true, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) }, { 3, "GOVERNANCE_EVENT", "Governance or regulatory event", true, true, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero) } }, schema: "public");
        migrationBuilder.InsertData("Phase5ImpactRule", new[] { "Id", "EventTypeRuleId", "SourceEntityType", "SourceEntityKey", "TargetEntityType", "TargetEntityKey", "TransmissionMechanism", "Direction", "Strength", "TimeHorizon", "MalaysiaRelevant", "IsActive", "Version", "EffectiveFrom", "CreatedAt", "CreatedByUserId" }, new object[,] { { 1L, 1, "Rate", "POLICY_RATE", "Sector", "BANKING", "Funding cost and credit demand", "Mixed", "Moderate", "MediumTerm", true, true, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "system" }, { 2L, 2, "Commodity", "CRUDE_PALM_OIL", "Sector", "PLANTATION", "Export revenue and margin", "Positive", "Moderate", "ShortTerm", true, true, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "system" }, { 3L, 3, "Event", "REGULATORY_ACTION", "Portfolio", "EXPOSURE_REVIEW", "Uncertainty and valuation sensitivity", "Negative", "Material", "ShortTerm", true, true, 1, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), "system" } }, schema: "public");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("Phase5ImpactRule", "public");
        migrationBuilder.DropTable("Phase5EventTypeRule", "public");
        migrationBuilder.DropTable("InquiryTelemetryEvent", "public");
        migrationBuilder.DropTable("InquiryOutcome", "public");
        migrationBuilder.DropTable("InquirySelectionEvent", "public");
        migrationBuilder.DropTable("InquiryEntityLink", "public");
    }
}
