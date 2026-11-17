using System.Security.Cryptography;
using System.Text;
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
    /// The attempt identifier for one notification to one recipient on one channel.
    /// </summary>
    /// <param name="notificationId">Identifies the notification, normally the domain event identifier.</param>
    /// <param name="recipientReference">The recipient's reference.</param>
    /// <param name="channel">The channel.</param>
    /// <returns>The same identifier every time for the same inputs, so a repeated send reuses it and the receiving side can deduplicate.</returns>
    public static Guid AttemptIdFor(Guid notificationId, string recipientReference, NotificationChannel channel)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{notificationId:N}|{recipientReference}|{channel}"));
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // version 5 style: name-based
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(bytes);
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
    /// <param name="notificationId">
    /// Identifies the notification, normally the domain event identifier. When given, attempt identifiers are derived from it
    /// (<see cref="AttemptIdFor"/>), so sending the same notification again carries the same identifiers; otherwise they are random.
    /// </param>
    /// <param name="alreadySent">
    /// Tells whether an attempt identifier was already accepted by its provider. Such an attempt is not sent again and is
    /// returned with status <see cref="NotificationAttemptStatus.Sent"/>, so retrying a notification resends only the
    /// channels that failed. Applies only when <paramref name="notificationId"/> is given.
    /// </param>
    /// <returns>One attempt per enabled provider that could reach the recipient; empty when none could.</returns>
    /// <remarks>
    /// Every template variant is rendered before anything is sent, so a missing template or value fails the whole
    /// notification rather than leaving it half sent. A provider that throws is recorded as a failed attempt and does
    /// not prevent the other providers from sending.
    /// </remarks>
    /// <exception cref="FileNotFoundException">The template does not exist.</exception>
    /// <exception cref="KeyNotFoundException">A placeholder value is missing.</exception>
    public async Task<IReadOnlyList<NotificationAttempt>> SendAsync(
        Guid caseId,
        Recipient recipient,
        string templateKey,
        string domainEventName,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct,
        Guid? notificationId = null,
        Func<Guid, bool>? alreadySent = null)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(values);

        var targets = new List<(INotificationProvider Provider, ContactPoint? ContactPoint, string? CannotReach)>();
        foreach (var provider in EnabledProviders(domainEventName))
        {
            try
            {
                if (provider.CanReach(recipient))
                {
                    targets.Add((provider, recipient.ContactPoints.FirstOrDefault(c => c.Channel == provider.Channel), null));
                }
            }
            catch (Exception ex)
            {
                targets.Add((provider, null, $"{provider.Channel} provider could not check the recipient: {ex.Message}"));
            }
        }

        var rendered = new Dictionary<bool, RenderedTemplate>();
        foreach (var smsSafe in targets.Where(t => t.CannotReach is null).Select(t => !t.Provider.AllowsSensitiveContent).Distinct())
        {
            rendered[smsSafe] = _templates.Render(templateKey, recipient.Language, smsSafe, values);
        }

        var attempts = new List<NotificationAttempt>(targets.Count);
        foreach (var (provider, contactPoint, cannotReach) in targets)
        {
            var attemptId = notificationId is { } id ? AttemptIdFor(id, recipient.Reference, provider.Channel) : Guid.NewGuid();
            var smsSafe = !provider.AllowsSensitiveContent;
            if (cannotReach is not null || contactPoint is null)
            {
                attempts.Add(Failed(attemptId, provider.Channel, smsSafe, cannotReach ?? $"The recipient has no {provider.Channel} contact point."));
                continue;
            }

            var template = rendered[smsSafe];
            if (notificationId is not null && alreadySent?.Invoke(attemptId) == true)
            {
                attempts.Add(new NotificationAttempt
                {
                    Id = attemptId,
                    Channel = provider.Channel,
                    AttemptedAt = _clock.UtcNow,
                    Status = NotificationAttemptStatus.Sent,
                    UsedSmsSafeTemplate = template.IsSmsSafe,
                });
                continue;
            }

            var message = new NotificationMessage
            {
                AttemptId = attemptId,
                CaseId = caseId,
                Recipient = recipient,
                ContactPoint = contactPoint,
                TemplateKey = templateKey,
                Subject = template.Subject,
                Body = template.Body,
                IsSmsSafe = template.IsSmsSafe,
            };

            SendResult result;
            try
            {
                result = await provider.SendAsync(message, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                result = new SendResult { Accepted = false, FailureReason = $"{provider.Channel} provider failed: {ex.Message}" };
            }

            attempts.Add(new NotificationAttempt
            {
                Id = attemptId,
                Channel = provider.Channel,
                AttemptedAt = _clock.UtcNow,
                Status = result.Accepted ? NotificationAttemptStatus.Sent : NotificationAttemptStatus.Failed,
                UsedSmsSafeTemplate = template.IsSmsSafe,
                ProviderMessageId = result.ProviderMessageId,
                FailureReason = result.Accepted ? null : result.FailureReason ?? "The provider did not accept the message.",
            });
        }

        return attempts;
    }

    /// <summary>
    /// Lists the channels a notification to this recipient would be sent on.
    /// </summary>
    /// <param name="recipient">Who to notify.</param>
    /// <param name="domainEventName">Name of the domain event; selects a per-event provider override.</param>
    /// <returns>Each enabled channel whose provider can reach the recipient, in channel order, as <see cref="SendAsync"/> would use them.</returns>
    /// <remarks>A provider that fails while checking the recipient is left out: <see cref="SendAsync"/> records it as a failed attempt.</remarks>
    public IReadOnlyList<NotificationChannel> ReachableChannels(Recipient recipient, string domainEventName)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        var channels = new List<NotificationChannel>();
        foreach (var provider in EnabledProviders(domainEventName))
        {
            try
            {
                if (provider.CanReach(recipient) && recipient.ContactPoints.Any(c => c.Channel == provider.Channel))
                {
                    channels.Add(provider.Channel);
                }
            }
            catch (Exception)
            {
                // Recorded as a failed attempt when the message is sent.
            }
        }

        return channels;
    }

    private NotificationAttempt Failed(Guid id, NotificationChannel channel, bool smsSafe, string reason) => new()
    {
        Id = id,
        Channel = channel,
        AttemptedAt = _clock.UtcNow,
        Status = NotificationAttemptStatus.Failed,
        UsedSmsSafeTemplate = smsSafe,
        FailureReason = reason,
    };

    private IEnumerable<INotificationProvider> EnabledProviders(string domainEventName)
    {
        var options = _options.Value;
        var names = options.PerEventOverrides.TryGetValue(domainEventName, out var overridden)
            ? overridden
            : options.EnabledProviders;
        var enabled = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);

        // One provider per channel, in channel order, so the outcome never depends on registration order.
        return _providers
            .Where(p => enabled.Contains(p.Channel.ToString()))
            .GroupBy(p => p.Channel)
            .OrderBy(g => g.Key)
            .Select(g => g.First());
    }
}
