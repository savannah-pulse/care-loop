using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLoop.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveHandoffSummaries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HandoffSummaries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HandoffSummaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DraftedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    MissingOrConflictingFactsJson = table.Column<string>(type: "jsonb", nullable: false),
                    ModelDeployment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    RejectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RejectedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionsJson = table.Column<string>(type: "jsonb", nullable: false),
                    SourceFactReferencesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HandoffSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HandoffSummaries_CaseId",
                table: "HandoffSummaries",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_HandoffSummaries_CaseId_Status",
                table: "HandoffSummaries",
                columns: new[] { "CaseId", "Status" });
        }
    }
}
