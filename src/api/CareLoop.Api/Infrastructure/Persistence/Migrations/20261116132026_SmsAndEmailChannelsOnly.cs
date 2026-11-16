using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareLoop.Api.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Rewrites stored uses of the removed <c>Simulator</c> notification channel as <c>Sms</c>.
    /// </summary>
    /// <remarks>
    /// The schema does not change. The channel is stored by name in two columns and inside the payload of stored
    /// source events, which the notification pipeline reads back; a name that is no longer a channel would stop
    /// those rows from loading. Audit rows are history and are left exactly as they were written.
    /// </remarks>
    public partial class SmsAndEmailChannelsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "NotificationAttempts" SET "Channel" = 'Sms' WHERE "Channel" = 'Simulator';""");
            migrationBuilder.Sql("""UPDATE "Cases" SET "CaregiverConsent_CaregiverContactPoint_Channel" = 'Sms' WHERE "CaregiverConsent_CaregiverContactPoint_Channel" = 'Simulator';""");
            migrationBuilder.Sql("""UPDATE "SourceEvents" SET "PayloadJson" = REPLACE("PayloadJson", '"channel":"Simulator"', '"channel":"Sms"') WHERE "PayloadJson" LIKE '%"channel":"Simulator"%';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: once rewritten, a text-message contact cannot be told from a former simulator one.
        }
    }
}
