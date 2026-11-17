namespace MessagingSimulator.Outbound;

/// <summary>
/// Where CareLoop can be reached, bound from the <c>CareLoop</c> configuration section.
/// </summary>
/// <remarks>
/// The simulator reaches CareLoop in one way only: it calls CareLoop back with delivery reports and
/// replies, the way an SMS or email service calls its customer back. It reads nothing from
/// CareLoop. It must never be pointed at a live endpoint.
/// </remarks>
public sealed class CareLoopOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string SectionName = "CareLoop";

    /// <summary>
    /// Address CareLoop exposes for provider callbacks, without the channel. Delivery reports and
    /// replies about a text message are posted to <c>{CallbackBaseUrl}/sms</c> and about an email to
    /// <c>{CallbackBaseUrl}/email</c>.
    /// </summary>
    public string CallbackBaseUrl { get; set; } = "http://localhost:5080/notifications/inbound";

    /// <summary>
    /// Shared secret every callback is signed with. It must equal CareLoop's
    /// <c>Notifications:Sms:InboundSecret</c> and <c>Notifications:Email:InboundSecret</c>.
    /// </summary>
    /// <remarks>
    /// CareLoop refuses an unsigned callback, so that nobody who merely knows a case identifier
    /// can acknowledge for a patient. There is no default: with no secret the simulator refuses
    /// to send, instead of sending something CareLoop is certain to reject. It is a local
    /// development secret for synthetic data, never a production credential.
    /// </remarks>
    public string InboundSecret { get; set; } = string.Empty;

    /// <summary>
    /// How many times a callback is sent in total when CareLoop answers with a server error or
    /// does not answer in time. A resend is safe because CareLoop deduplicates callbacks.
    /// </summary>
    public int SendAttempts { get; set; } = 3;

    /// <summary>Pause before each further attempt, in milliseconds, multiplied by the number of attempts already made.</summary>
    public int RetryDelayMilliseconds { get; set; } = 200;

    /// <summary>
    /// How long one call to CareLoop may take, in seconds, before it counts as not answered.
    /// </summary>
    /// <remarks>
    /// A delivery report is sent while CareLoop is still waiting for the answer to the
    /// message it posted. All attempts together must therefore finish well
    /// inside CareLoop's own HTTP timeout, or CareLoop gives up on a request the simulator is
    /// still handling.
    /// </remarks>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// How many more times the simulator reports a delivery outcome by itself, in the background,
    /// after the report failed while the notification was being received.
    /// </summary>
    /// <remarks>
    /// CareLoop does not send a patient notification again, so a report that fails once would
    /// otherwise leave the message undelivered for good. Zero switches the background retries off.
    /// </remarks>
    public int DeliveryReportRetries { get; set; } = 10;

    /// <summary>
    /// Seconds before the first background retry of a delivery report. Each further retry waits
    /// twice as long as the one before, up to <see cref="DeliveryReportRetryMaximumSeconds"/>.
    /// </summary>
    public int DeliveryReportRetrySeconds { get; set; } = 5;

    /// <summary>The longest wait between two background retries of a delivery report, in seconds.</summary>
    public int DeliveryReportRetryMaximumSeconds { get; set; } = 60;
}
