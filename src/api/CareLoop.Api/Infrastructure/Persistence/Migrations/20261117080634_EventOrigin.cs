using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLoop.Api.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Records where each stored event came from: CareLoop itself, a text message or an email channel, or (when empty)
    /// the case's own source system.
    /// </summary>
    /// <remarks>
    /// Existing rows are filled in from what their identifiers already say: callbacks were stored as <c>sms/…</c> or
    /// <c>email/…</c>, a nurse's call outcome as <c>call-outcome/…</c>, CareLoop's record of its own sends as
    /// <c>notification-attempt/…</c>, and CareLoop's own events under the <c>careloop</c> source system.
    /// </remarks>
    public partial class EventOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Origin",
                table: "SourceEvents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql("""UPDATE "SourceEvents" SET "Origin" = 'sms' WHERE "SourceEventId" LIKE 'sms/%';""");
            migrationBuilder.Sql("""UPDATE "SourceEvents" SET "Origin" = 'email' WHERE "SourceEventId" LIKE 'email/%';""");
            migrationBuilder.Sql("""UPDATE "SourceEvents" SET "Origin" = 'careloop' WHERE "SourceEventId" LIKE 'call-outcome/%' OR "SourceEventId" LIKE 'notification-attempt/%' OR "SourceSystem" = 'careloop';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Origin",
                table: "SourceEvents");
        }
    }
}
