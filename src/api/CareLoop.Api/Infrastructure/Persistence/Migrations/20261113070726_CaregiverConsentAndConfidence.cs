using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLoop.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaregiverConsentAndConfidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaregiverConsent_CaregiverContactPoint_Address",
                table: "Cases",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaregiverConsent_CaregiverContactPoint_Channel",
                table: "Cases",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaregiverConsent_EvidenceReference",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CaregiverConsent_GrantedAt",
                table: "Cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaregiverConsent_WithdrawalEvidenceReference",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CaregiverConsent_WithdrawnAt",
                table: "Cases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Notification_Confidence",
                table: "Cases",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaregiverConsent_CaregiverContactPoint_Address",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaregiverConsent_CaregiverContactPoint_Channel",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaregiverConsent_EvidenceReference",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaregiverConsent_GrantedAt",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaregiverConsent_WithdrawalEvidenceReference",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "CaregiverConsent_WithdrawnAt",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "Notification_Confidence",
                table: "Cases");
        }
    }
}
