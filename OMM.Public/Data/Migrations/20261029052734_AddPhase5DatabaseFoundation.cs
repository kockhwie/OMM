using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OMM.Public.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase5DatabaseFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "AiPromptMode",
                schema: "public",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsAdvisory = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPromptMode", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "InquiryPurpose",
                schema: "public",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DisplayNameEn = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayNameZh = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DescriptionEn = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DescriptionZh = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquiryPurpose", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "InquirySession",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    EntryPoint = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RequestedMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    FlowVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquirySession", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InquirySession_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionLifecycleJobRun",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TriggerSource = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TriggeredByAdminId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScannedCount = table.Column<int>(type: "integer", nullable: false),
                    ActivatedCount = table.Column<int>(type: "integer", nullable: false),
                    ExpiredCount = table.Column<int>(type: "integer", nullable: false),
                    DowngradedCount = table.Column<int>(type: "integer", nullable: false),
                    NotifiedCount = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ErrorSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionLifecycleJobRun", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionLifecycleJobRun_Status", "\"Status\" IN ('Running','Succeeded','Failed','Partial')");
                });

            migrationBuilder.CreateTable(
                name: "AiPromptVersion",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptModeCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SystemInstruction = table.Column<string>(type: "text", nullable: false),
                    UserMessageTemplate = table.Column<string>(type: "text", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPromptVersion", x => x.Id);
                    table.CheckConstraint("CK_AiPromptVersion_Status", "\"Status\" IN ('Draft','Active','Inactive','Archived')");
                    table.ForeignKey(
                        name: "FK_AiPromptVersion_AiPromptMode_PromptModeCode",
                        column: x => x.PromptModeCode,
                        principalSchema: "public",
                        principalTable: "AiPromptMode",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlan",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    PromptModeCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DurationMonths = table.Column<int>(type: "integer", nullable: true),
                    PriceAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    PaymentInstructions = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlan", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionPlan_DurationMonths", "\"DurationMonths\" IS NULL OR \"DurationMonths\" > 0");
                    table.CheckConstraint("CK_SubscriptionPlan_PriceAmount", "\"PriceAmount\" IS NULL OR \"PriceAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_SubscriptionPlan_AiPromptMode_PromptModeCode",
                        column: x => x.PromptModeCode,
                        principalSchema: "public",
                        principalTable: "AiPromptMode",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AiPromptAuditEvent",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PromptVersionId = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    ChangeMetadataJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiPromptAuditEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiPromptAuditEvent_AiPromptVersion_PromptVersionId",
                        column: x => x.PromptVersionId,
                        principalSchema: "public",
                        principalTable: "AiPromptVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InquiryTurn",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InquirySessionId = table.Column<long>(type: "bigint", nullable: false),
                    TurnNumber = table.Column<int>(type: "integer", nullable: false),
                    PromptHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DetectedIntent = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    EventType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    VerificationStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Confidence = table.Column<decimal>(type: "numeric(6,5)", precision: 6, scale: 5, nullable: true),
                    PromptVersionId = table.Column<int>(type: "integer", nullable: true),
                    UsedEmergencyPrompt = table.Column<bool>(type: "boolean", nullable: false),
                    RequestedMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EffectiveMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FallbackReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AiCallAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquiryTurn", x => x.Id);
                    table.CheckConstraint("CK_InquiryTurn_AiCallAttemptCount", "\"AiCallAttemptCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_InquiryTurn_AiPromptVersion_PromptVersionId",
                        column: x => x.PromptVersionId,
                        principalSchema: "public",
                        principalTable: "AiPromptVersion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InquiryTurn_InquirySession_InquirySessionId",
                        column: x => x.InquirySessionId,
                        principalSchema: "public",
                        principalTable: "InquirySession",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberSubscription",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    ManuallyGranted = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EntitlementSlot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubscriptionExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TerminatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedByUserId = table.Column<string>(type: "text", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberSubscription", x => x.Id);
                    table.CheckConstraint("CK_MemberSubscription_Expiry", "\"SubscriptionExpiryDate\" IS NULL OR \"SubscriptionExpiryDate\" > \"StartedAt\"");
                    table.CheckConstraint("CK_MemberSubscription_Slot", "(\"Status\" = 'Active' AND \"EntitlementSlot\" = 'Current') OR (\"Status\" = 'Scheduled' AND \"EntitlementSlot\" = 'Scheduled') OR (\"Status\" = 'Suspended' AND \"EntitlementSlot\" IN ('Current','Scheduled')) OR (\"Status\" IN ('Expired','Cancelled','Terminated') AND \"EntitlementSlot\" IS NULL)");
                    table.CheckConstraint("CK_MemberSubscription_Status", "\"Status\" IN ('Scheduled','Active','Suspended','Expired','Cancelled','Terminated')");
                    table.ForeignKey(
                        name: "FK_MemberSubscription_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MemberSubscription_SubscriptionPlan_SubscriptionPlanId",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "public",
                        principalTable: "SubscriptionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InquiryRawMessage",
                schema: "public",
                columns: table => new
                {
                    InquiryTurnId = table.Column<long>(type: "bigint", nullable: false),
                    MessageText = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InquiryRawMessage", x => x.InquiryTurnId);
                    table.ForeignKey(
                        name: "FK_InquiryRawMessage_InquiryTurn_InquiryTurnId",
                        column: x => x.InquiryTurnId,
                        principalSchema: "public",
                        principalTable: "InquiryTurn",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionChangeEvent",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    SubscriptionId = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<string>(type: "text", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    PreviousStateJson = table.Column<string>(type: "jsonb", nullable: true),
                    NewStateJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionChangeEvent", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionChangeEvent_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriptionChangeEvent_MemberSubscription_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "public",
                        principalTable: "MemberSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionDowngradeEvent",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    SourceSubscriptionId = table.Column<long>(type: "bigint", nullable: false),
                    PreviousPlanId = table.Column<int>(type: "integer", nullable: false),
                    ResultingPlanId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    NotificationStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionDowngradeEvent", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionDowngradeEvent_NotificationStatus", "\"NotificationStatus\" IN ('Pending','Sent','Failed')");
                    table.ForeignKey(
                        name: "FK_SubscriptionDowngradeEvent_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriptionDowngradeEvent_MemberSubscription_SourceSubscri~",
                        column: x => x.SourceSubscriptionId,
                        principalSchema: "public",
                        principalTable: "MemberSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionDowngradeEvent_SubscriptionPlan_PreviousPlanId",
                        column: x => x.PreviousPlanId,
                        principalSchema: "public",
                        principalTable: "SubscriptionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionDowngradeEvent_SubscriptionPlan_ResultingPlanId",
                        column: x => x.ResultingPlanId,
                        principalSchema: "public",
                        principalTable: "SubscriptionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionUpgradeRequest",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    SubscriptionPlanId = table.Column<int>(type: "integer", nullable: false),
                    AmountDue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedByUserId = table.Column<string>(type: "text", nullable: true),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                    ResultingSubscriptionId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionUpgradeRequest", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionUpgradeRequest_AmountDue", "\"AmountDue\" >= 0");
                    table.CheckConstraint("CK_SubscriptionUpgradeRequest_PaymentReference", "\"Status\" <> 'PaymentSubmitted' OR NULLIF(BTRIM(\"PaymentReference\"), '') IS NOT NULL");
                    table.CheckConstraint("CK_SubscriptionUpgradeRequest_Status", "\"Status\" IN ('AwaitingPayment','PaymentSubmitted','Approved','Rejected','Withdrawn')");
                    table.ForeignKey(
                        name: "FK_SubscriptionUpgradeRequest_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SubscriptionUpgradeRequest_MemberSubscription_ResultingSubs~",
                        column: x => x.ResultingSubscriptionId,
                        principalSchema: "public",
                        principalTable: "MemberSubscription",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionUpgradeRequest_SubscriptionPlan_SubscriptionPla~",
                        column: x => x.SubscriptionPlanId,
                        principalSchema: "public",
                        principalTable: "SubscriptionPlan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "AiPromptMode",
                columns: new[] { "Code", "DisplayName", "IsActive", "IsAdvisory", "SchemaVersion" },
                values: new object[,]
                {
                    { "PRO", "Pro", true, true, "v1" },
                    { "STANDARD", "Standard", true, false, "v1" }
                });

            migrationBuilder.InsertData(
                table: "SystemSetting",
                columns: new[] { "Key", "Description", "ModifiedAt", "ModifiedByUserId", "Value" },
                values: new object[,]
                {
                    { "InquiryRetentionYears", "Retention period for inquiry records and raw messages, in calendar years.", null, null, "3" },
                    { "PublicProEnabled", "Enables Pro mode for Public members. Default is enabled; Admin may change this setting.", null, null, "true" }
                });

            migrationBuilder.InsertData(
                schema: "public",
                table: "SubscriptionPlan",
                columns: new[] { "Id", "Code", "CreatedAt", "CurrencyCode", "Description", "DisplayName", "DurationMonths", "IsActive", "ModifiedAt", "PaymentInstructions", "PriceAmount", "PromptModeCode", "SortOrder" },
                values: new object[,]
                {
                    { 1, "STANDARD", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "Lifetime educational access.", "Standard", null, true, null, null, null, "STANDARD", 1 },
                    { 2, "PRO", new DateTimeOffset(new DateTime(2026, 10, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "MYR", "Personalized Pro Advice for 12 months.", "Pro", 12, true, null, null, 99m, "PRO", 2 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiPromptAuditEvent_PromptVersionId_OccurredAt",
                schema: "public",
                table: "AiPromptAuditEvent",
                columns: new[] { "PromptVersionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiPromptVersion_PromptModeCode_Status",
                schema: "public",
                table: "AiPromptVersion",
                columns: new[] { "PromptModeCode", "Status" },
                unique: true,
                filter: "\"Status\" = 'Active'");

            migrationBuilder.CreateIndex(
                name: "IX_InquiryPurpose_IsActive_SortOrder",
                schema: "public",
                table: "InquiryPurpose",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_InquiryRawMessage_ExpiresAt",
                schema: "public",
                table: "InquiryRawMessage",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_InquirySession_ExpiresAt",
                schema: "public",
                table: "InquirySession",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_InquirySession_UserId_StartedAt",
                schema: "public",
                table: "InquirySession",
                columns: new[] { "UserId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InquiryTurn_CreatedAt",
                schema: "public",
                table: "InquiryTurn",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InquiryTurn_InquirySessionId_TurnNumber",
                schema: "public",
                table: "InquiryTurn",
                columns: new[] { "InquirySessionId", "TurnNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InquiryTurn_PromptVersionId",
                schema: "public",
                table: "InquiryTurn",
                column: "PromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSubscription_Status_SubscriptionExpiryDate",
                schema: "public",
                table: "MemberSubscription",
                columns: new[] { "Status", "SubscriptionExpiryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MemberSubscription_SubscriptionPlanId",
                schema: "public",
                table: "MemberSubscription",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSubscription_UserId_EntitlementSlot",
                schema: "public",
                table: "MemberSubscription",
                columns: new[] { "UserId", "EntitlementSlot" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"EntitlementSlot\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberSubscription_UserId_Status_StartedAt",
                schema: "public",
                table: "MemberSubscription",
                columns: new[] { "UserId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionChangeEvent_SubscriptionId_OccurredAt",
                schema: "public",
                table: "SubscriptionChangeEvent",
                columns: new[] { "SubscriptionId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionChangeEvent_UserId_OccurredAt",
                schema: "public",
                table: "SubscriptionChangeEvent",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDowngradeEvent_NotificationStatus_DetectedAt",
                schema: "public",
                table: "SubscriptionDowngradeEvent",
                columns: new[] { "NotificationStatus", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDowngradeEvent_PreviousPlanId",
                schema: "public",
                table: "SubscriptionDowngradeEvent",
                column: "PreviousPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDowngradeEvent_ResultingPlanId",
                schema: "public",
                table: "SubscriptionDowngradeEvent",
                column: "ResultingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDowngradeEvent_SourceSubscriptionId",
                schema: "public",
                table: "SubscriptionDowngradeEvent",
                column: "SourceSubscriptionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionDowngradeEvent_UserId",
                schema: "public",
                table: "SubscriptionDowngradeEvent",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLifecycleJobRun_StartedAt",
                schema: "public",
                table: "SubscriptionLifecycleJobRun",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionLifecycleJobRun_Status_StartedAt",
                schema: "public",
                table: "SubscriptionLifecycleJobRun",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlan_Code",
                schema: "public",
                table: "SubscriptionPlan",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlan_IsActive_SortOrder",
                schema: "public",
                table: "SubscriptionPlan",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlan_PromptModeCode",
                schema: "public",
                table: "SubscriptionPlan",
                column: "PromptModeCode");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpgradeRequest_ResultingSubscriptionId",
                schema: "public",
                table: "SubscriptionUpgradeRequest",
                column: "ResultingSubscriptionId",
                unique: true,
                filter: "\"ResultingSubscriptionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpgradeRequest_SubscriptionPlanId",
                schema: "public",
                table: "SubscriptionUpgradeRequest",
                column: "SubscriptionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpgradeRequest_UserId_CreatedAt",
                schema: "public",
                table: "SubscriptionUpgradeRequest",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUpgradeRequest_UserId_SubscriptionPlanId",
                schema: "public",
                table: "SubscriptionUpgradeRequest",
                columns: new[] { "UserId", "SubscriptionPlanId" },
                unique: true,
                filter: "\"UserId\" IS NOT NULL AND \"Status\" IN ('AwaitingPayment','PaymentSubmitted')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiPromptAuditEvent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InquiryPurpose",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InquiryRawMessage",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SubscriptionChangeEvent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SubscriptionDowngradeEvent",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SubscriptionLifecycleJobRun",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SubscriptionUpgradeRequest",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InquiryTurn",
                schema: "public");

            migrationBuilder.DropTable(
                name: "MemberSubscription",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AiPromptVersion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "InquirySession",
                schema: "public");

            migrationBuilder.DropTable(
                name: "SubscriptionPlan",
                schema: "public");

            migrationBuilder.DropTable(
                name: "AiPromptMode",
                schema: "public");

            migrationBuilder.DeleteData(
                table: "SystemSetting",
                keyColumn: "Key",
                keyValue: "InquiryRetentionYears");

            migrationBuilder.DeleteData(
                table: "SystemSetting",
                keyColumn: "Key",
                keyValue: "PublicProEnabled");
        }
    }
}
