using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CareLoop.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Disposition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedTimestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PriorProjectionJson = table.Column<string>(type: "text", nullable: true),
                    NewProjectionJson = table.Column<string>(type: "text", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseReads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorFacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    View = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Query = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OutsideTeam = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseReads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceResultId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PatientReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PatientDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PatientSecondaryIdentifier = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PathwayId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Stage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Owner_Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Owner_Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Owner_FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Owner_IsQueue = table.Column<bool>(type: "boolean", nullable: false),
                    PendingAssignment_ProposedOwner_Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PendingAssignment_ProposedOwner_Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PendingAssignment_ProposedOwner_FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PendingAssignment_ProposedOwner_IsQueue = table.Column<bool>(type: "boolean", nullable: true),
                    PendingAssignment_ProposedBy_Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PendingAssignment_ProposedBy_Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PendingAssignment_ProposedBy_FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PendingAssignment_ProposedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PendingAssignment_RespondBy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PendingAssignment_Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PendingAssignment_RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PendingAssignment_RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    NextAction = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEscalated = table.Column<bool>(type: "boolean", nullable: false),
                    Exceptions = table.Column<int>(type: "integer", nullable: false),
                    Notification_Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notification_SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notification_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notification_UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notification_CallRequested = table.Column<bool>(type: "boolean", nullable: true),
                    Notification_Confidence = table.Column<int>(type: "integer", nullable: true),
                    Consultation_Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Consultation_SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Consultation_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Consultation_UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Referral_Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Referral_SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Referral_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Referral_UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Referral_RejectionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Appointment_Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Appointment_SourceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Appointment_StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Appointment_UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Appointment_ScheduledFor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Appointment_OutcomeReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OutcomeEvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OutcomeEvidenceKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Closure_Reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Closure_AuthorizedBy_Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Closure_AuthorizedBy_Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Closure_AuthorizedBy_FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Closure_DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Closure_EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Closure_ExceptionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Closure_NextSurveillanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Closure_DocumentedAttemptReferences = table.Column<string>(type: "text", nullable: true),
                    CaregiverConsent_CaregiverContactPoint_Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CaregiverConsent_CaregiverContactPoint_Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CaregiverConsent_GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CaregiverConsent_WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CaregiverConsent_EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CaregiverConsent_WithdrawalEvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HandoffSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    SectionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    MissingOrConflictingFactsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceFactReferencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    DraftedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ModelDeployment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DraftedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoffSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SourceEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceEventId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor_Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Actor_Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Actor_FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Disposition = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DispositionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EvidenceReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseEscalations",
                columns: table => new
                {
                    Sequence = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    EscalatedTo_Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EscalatedTo_Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EscalatedTo_FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EscalatedTo_IsQueue = table.Column<bool>(type: "boolean", nullable: false),
                    RaisedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AcknowledgedBy_Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcknowledgedBy_Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AcknowledgedBy_FacilityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEscalations", x => x.Sequence);
                    table.ForeignKey(
                        name: "FK_CaseEscalations_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    UsedSmsSafeTemplate = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationAttempts_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subscriber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LeaseOwner = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeasedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OutboxDeliveries_OutboxMessages_OutboxMessageId",
                        column: x => x.OutboxMessageId,
                        principalTable: "OutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_CaseId_ReceivedTimestamp",
                table: "AuditEvents",
                columns: new[] { "CaseId", "ReceivedTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_SourceEventId",
                table: "AuditEvents",
                column: "SourceEventId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEscalations_CaseId",
                table: "CaseEscalations",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEscalations_Id",
                table: "CaseEscalations",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReads_ActorReference_ReadAt",
                table: "CaseReads",
                columns: new[] { "ActorReference", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseReads_CaseId_ReadAt",
                table: "CaseReads",
                columns: new[] { "CaseId", "ReadAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_DueAt",
                table: "Cases",
                column: "DueAt");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_SourceSystem_SourceResultId",
                table: "Cases",
                columns: new[] { "SourceSystem", "SourceResultId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Stage",
                table: "Cases",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffSummaries_CaseId",
                table: "HandoffSummaries",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffSummaries_CaseId_Status",
                table: "HandoffSummaries",
                columns: new[] { "CaseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAttempts_CaseId",
                table: "NotificationAttempts",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_OutboxMessageId_Subscriber",
                table: "OutboxDeliveries",
                columns: new[] { "OutboxMessageId", "Subscriber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_Status_NextAttemptAt",
                table: "OutboxDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CaseId",
                table: "OutboxMessages",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceEvents_CaseId",
                table: "SourceEvents",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_SourceEvents_SourceSystem_SourceEventId",
                table: "SourceEvents",
                columns: new[] { "SourceSystem", "SourceEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "CaseEscalations");

            migrationBuilder.DropTable(
                name: "CaseReads");

            migrationBuilder.DropTable(
                name: "HandoffSummaries");

            migrationBuilder.DropTable(
                name: "NotificationAttempts");

            migrationBuilder.DropTable(
                name: "OutboxDeliveries");

            migrationBuilder.DropTable(
                name: "SourceEvents");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "OutboxMessages");
        }
    }
}
