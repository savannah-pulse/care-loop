using Microsoft.Extensions.Options;
using MessagingSimulator.Outbound;
using MessagingSimulator.Scenarios;
using MessagingSimulator.State;

namespace MessagingSimulator.Inbound;

/// <summary>
/// Reports a delivery outcome again, in the background, after the first report failed.
/// </summary>
/// <remarks>
/// CareLoop sends a patient notification once: a failed send is recorded on the case, not sent
/// again. So when the delivery report cannot be made while the notification is being received,
/// nobody else will ever finish the delivery. A real channel accepts the message and reports
/// later; this does the same. Retries back off and stop after
/// <see cref="CareLoopOptions.DeliveryReportRetries"/>, after which the message stays unreported
/// and CareLoop's own deadline has to notice.
/// </remarks>
public sealed class DeliveryReportRetrier
{
    private readonly DeliverySimulator _delivery;
    private readonly AutomaticResponder _responder;
    private readonly SimulatorStore _store;
    private readonly MessageGate _gate;
    private readonly ISimulatorClock _clock;
    private readonly IOptions<CareLoopOptions> _options;

    /// <summary>
    /// Creates the retrier.
    /// </summary>
    /// <param name="delivery">Reports the delivery outcome.</param>
    /// <param name="responder">Lets the patient react once the message is delivered.</param>
    /// <param name="store">Holds the queued reports and the inbox.</param>
    /// <param name="gate">Keeps a retry and a resend from reporting the same message at once.</param>
    /// <param name="clock">Source of the due times.</param>
    /// <param name="options">Supplies how often and how long to retry.</param>
    public DeliveryReportRetrier(
        DeliverySimulator delivery,
        AutomaticResponder responder,
        SimulatorStore store,
        MessageGate gate,
        ISimulatorClock clock,
        IOptions<CareLoopOptions> options)
    {
        _delivery = delivery;
        _responder = responder;
        _store = store;
        _gate = gate;
        _clock = clock;
        _options = options;
    }

    /// <summary>
    /// Queues the first background retry for a message whose delivery report just failed.
    /// </summary>
    /// <param name="messageId">Simulator-local identifier of the inbox message.</param>
    /// <returns><see langword="true"/> when a retry is now waiting; <see langword="false"/> when background retries are switched off.</returns>
    public bool ScheduleFirst(string messageId) => Schedule(messageId, attempt: 1);

    /// <summary>
    /// Tries the queued delivery reports that are due.
    /// </summary>
    /// <param name="ct">Cancels the pass.</param>
    /// <returns>How many reports were tried.</returns>
    public async Task<int> RetryDueAsync(CancellationToken ct)
    {
        var due = _store.ClaimDueDeliveryReports(_clock.UtcNow);
        foreach (var report in due)
        {
            InboxMessage? delivered = null;
            using (await _gate.EnterAsync(report.MessageId, ct))
            {
                // A resend from CareLoop may have finished the delivery in the meantime.
                var message = _store.FindInboxMessage(report.MessageId);
                if (message is not { Delivery: DeliveryState.Pending })
                {
                    continue;
                }

                try
                {
                    message = await _delivery.ApplyAsync(message, ct);
                    delivered = message.Delivery == DeliveryState.Delivered ? message : null;
                }
                catch (Exception ex) when (ex is HttpRequestException or CareLoopNotConfiguredException)
                {
                    // The failed attempt is already recorded as a reaction; wait longer and try again.
                    Schedule(report.MessageId, report.Attempt + 1);
                }
            }

            // Outside the gate, because responding enters it again.
            if (delivered is not null)
            {
                await _responder.ScheduleAsync(delivered, ct);
            }
        }

        return due.Count;
    }

    private bool Schedule(string messageId, int attempt)
    {
        var options = _options.Value;
        if (attempt > options.DeliveryReportRetries)
        {
            return false;
        }

        var first = Math.Max(0, options.DeliveryReportRetrySeconds);
        var longest = Math.Max(first, options.DeliveryReportRetryMaximumSeconds);
        var wait = Math.Min(longest, first * Math.Pow(2, Math.Min(attempt - 1, 30)));
        _store.ScheduleDeliveryReport(new PendingDeliveryReport(messageId, attempt, _clock.UtcNow + TimeSpan.FromSeconds(wait)));
        return true;
    }
}
