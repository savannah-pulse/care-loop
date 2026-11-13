using CareLoop.Domain.Recipients;
using CareLoop.Domain.Time;
using CareLoop.Domain.Tracks;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Features.Notifications;

/// <summary>
/// Sends one notification on every enabled channel that can reach the recipient.
/// </summary>
/// <remarks>
/// Redundancy is intended: a patient who misses one channel may see another. Each send is
/// recorded as its own <see cref="NotificationAttempt"/>; the notification counts as delivered if
/// any channel delivers and as failed only if all fail. A provider whose
/// <see cref="INotificationProvider.AllowsSensitiveContent"/> is <see langword="false"/> receives the
/// SMS-safe template. Sending never acknowledges anything, transfers ownership or stops a clock.
/// </remarks>
public sealed class NotificationSender
{
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly TemplateRenderer _templates;
    private readonly IOptions<NotificationOptions> _options;
    private readonly IClock _clock;

    /// <summary>
    /// Creates the sender.
    /// </summary>
    /// <param name="providers">Every registered provider; only those named in the options are used.</param>
    /// <param name="templates">Renders the approved templates per channel.</param>
    /// <param name="options">Which providers are enabled, with optional per-event overrides.</param>
    /// <param name="clock">Supplies attempt timestamps.</param>
    public NotificationSender(
        IEnumerable<INotificationProvider> providers,
        TemplateRenderer templates,
        IOptions<NotificationOptions> options,
        IClock clock)
    {
        _providers = providers;
        _templates = templates;
        _options = options;
        _clock = clock;
    }

    /// <summary>
    /// Sends a notification to one recipient.
    /// </summary>
    /// <param name="caseId">The case the notification is about.</param>
    /// <param name="recipient">Who to notify.</param>
    /// <param name="templateKey">Key of the approved template.</param>
    /// <param name="domainEventName">Name of the domain event that caused the notification; selects a per-event provider override.</param>
    /// <param name="values">Placeholder values for the template. Staff templates receive no patient detail.</param>
    /// <param name="ct">Cancels the operation.</param>
    /// <returns>One attempt per enabled provider that could reach the recipient; empty when none could.</returns>
    /// <remarks>A provider that throws is recorded as a failed attempt and does not prevent the other providers from sending.</remarks>
    public Task<IReadOnlyList<NotificationAttempt>> SendAsync(
        Guid caseId,
        Recipient recipient,
        string templateKey,
        string domainEventName,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct)
    {
        // TODO(WS-4): select enabled providers with CanReach, render per channel (SMS-safe where required), send, collect attempts.
        throw new NotImplementedException("WS4: NotificationSender.SendAsync");
    }
}
