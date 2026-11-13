using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLoop.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AppointmentOutcomeReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Appointment_OutcomeReference",
                table: "Cases",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Appointment_OutcomeReference",
                table: "Cases");
        }
    }
}
