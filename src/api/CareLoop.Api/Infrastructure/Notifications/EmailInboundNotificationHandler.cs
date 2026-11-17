using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Persistence;
using CareLoop.Domain.Recipients;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Receives what the simulator posts to <c>POST /notifications/inbound/email</c>: delivery
/// reports for email messages and the patient's replies to them.
/// </summary>
/// <remarks>The format and the rules are those of <see cref="SimulatorInboundNotificationHandler"/>.</remarks>
public sealed class EmailInboundNotificationHandler : SimulatorInboundNotificationHandler
{
    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="db">Read access to cases, to find the source identity of the case a callback is about.</param>
    /// <param name="recipients">Supplies the patient's own addresses, which a reply must come from.</param>
    /// <param name="options">Supplies the secret that inbound messages are signed with, one named set per channel.</param>
    public EmailInboundNotificationHandler(CareLoopDbContext db, RecipientResolver recipients, IOptionsMonitor<SimulatorNotificationOptions> options)
        : base(db, recipients, options)
    {
    }

    /// <inheritdoc/>
    public override NotificationChannel Channel => NotificationChannel.Email;
}
