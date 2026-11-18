using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLoop.Api.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Records, with each read made to ask an AI agent, the agent's name and pinned version, the prompt's version and the
    /// call's correlation identifier.
    /// </summary>
    /// <remarks>
    /// Nullable columns: every other read, and every read recorded before this migration, has none. Indexed by
    /// correlation identifier, to find the audit entry for one call from the logs.
    /// </remarks>
    public partial class AiCallAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgentName",
                table: "CaseReads",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentVersion",
                table: "CaseReads",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "CaseReads",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptVersion",
                table: "CaseReads",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CaseReads_CorrelationId",
                table: "CaseReads",
                column: "CorrelationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CaseReads_CorrelationId",
                table: "CaseReads");

            migrationBuilder.DropColumn(
                name: "AgentName",
                table: "CaseReads");

            migrationBuilder.DropColumn(
                name: "AgentVersion",
                table: "CaseReads");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "CaseReads");

            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "CaseReads");
        }
    }
}
