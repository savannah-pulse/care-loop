namespace PatientChannelSimulator.State;

/// <summary>
/// In-memory state of the patient-channel simulator: the simulated patient inbox, the FHIR
/// message log, scenario runs, patient behaviours, the reactions to notifications and the
/// responses that are waiting for their due time.
/// </summary>
/// <remarks>
/// Registered as a singleton and therefore shared by concurrent requests, the scenario runner and
/// the response dispatcher. One lock guards plain collections, because several operations must be
/// atomic across more than one of them (for example "store unless this request was already
/// received"). Every read returns a snapshot. Everything here is synthetic and is lost on restart
/// by design: the simulator is not a system of record.
/// </remarks>
public sealed class SimulatorStore
{
    /// <summary>The most log entries kept; the oldest are dropped beyond this.</summary>
    public const int LogCapacity = 500;

    private readonly Lock _gate = new();
    private readonly Dictionary<string, InboxMessage> _messages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _messageIdByRequest = new(StringComparer.Ordinal);
    private readonly List<string> _messageOrder = [];
    private readonly Queue<FhirMessageLogEntry> _log = new();
    private readonly Dictionary<string, PatientBehaviourAssignment> _behaviours = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _behaviourInForceFrom = new(StringComparer.Ordinal);
    private readonly List<PatientReactionRecord> _reactions = [];
    private readonly Dictionary<string, ScenarioRunState> _runs = new(StringComparer.Ordinal);
    private readonly List<PendingResponse> _pending = [];
    private readonly List<PendingDeliveryReport> _deliveryReports = [];
    private PatientBehaviour _defaultBehaviour = new(DeliveryBehaviour.Deliver, PatientResponseBehaviour.None);
    private int _defaultInForceFrom;

    /// <summary>
    /// Adds a newly received message to the simulated patient inbox.
    /// </summary>
    /// <param name="message">The message to store; its <see cref="InboxMessage.MessageId"/> must be unique.</param>
    /// <exception cref="InvalidOperationException">A message with the same identifier is already stored.</exception>
    public void AddInboxMessage(InboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            if (_messages.ContainsKey(message.MessageId))
            {
                throw new InvalidOperationException($"Inbox message '{message.MessageId}' is already stored.");
            }

            Store(message);
        }
    }

    /// <summary>
    /// Adds a newly received message unless its <c>CommunicationRequest</c> was already received.
    /// </summary>
    /// <remarks>
    /// The check and the insert are one atomic step, so two concurrent deliveries of the same
    /// request can never both create a message.
    /// </remarks>
    /// <param name="message">The message to store.</param>
    /// <param name="stored">The message now in the inbox: <paramref name="message"/> when it was added, otherwise the one stored earlier.</param>
    /// <returns><see langword="true"/> when the message was added; <see langword="false"/> when the request, or the message identifier, was seen before.</returns>
    public bool TryAddInboxMessage(InboxMessage message, out InboxMessage stored)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            if (_messageIdByRequest.TryGetValue(message.CommunicationRequestId, out var existingId))
            {
                stored = _messages[existingId];
                return false;
            }

            if (_messages.TryGetValue(message.MessageId, out var existing))
            {
                stored = existing;
                return false;
            }

            Store(message);
            stored = message;
            return true;
        }
    }

    /// <summary>
    /// Lists the simulated patient inbox.
    /// </summary>
    /// <returns>A snapshot of all inbox messages, newest first.</returns>
    public IReadOnlyList<InboxMessage> GetInboxMessages()
    {
        lock (_gate)
        {
            // Stable sort over arrival order reversed, so messages received at the same instant
            // still list the later arrival first.
            return [.. Enumerable.Reverse(_messageOrder).Select(id => _messages[id]).OrderByDescending(m => m.ReceivedAt)];
        }
    }

    /// <summary>
    /// Looks up one inbox message.
    /// </summary>
    /// <param name="messageId">Simulator-local identifier of the message.</param>
    /// <returns>The message, or <see langword="null"/> when no message has that identifier.</returns>
    public InboxMessage? FindInboxMessage(string messageId)
    {
        lock (_gate)
        {
            return _messages.GetValueOrDefault(messageId);
        }
    }

    /// <summary>
    /// Looks up the inbox message created from a given <c>CommunicationRequest</c>.
    /// </summary>
    /// <remarks>
    /// Used to make inbound handling idempotent: CareLoop's outbox may resend the same request,
    /// and a resend must not create a second inbox message.
    /// </remarks>
    /// <param name="communicationRequestId">Logical id of the FHIR <c>CommunicationRequest</c>.</param>
    /// <returns>The existing message, or <see langword="null"/> when the request has not been seen.</returns>
    public InboxMessage? FindInboxMessageByRequest(string communicationRequestId)
    {
        lock (_gate)
        {
            return _messageIdByRequest.TryGetValue(communicationRequestId, out var id) ? _messages[id] : null;
        }
    }

    /// <summary>
    /// Replaces a stored inbox message with an updated copy (for example after a delivery report
    /// or a patient response).
    /// </summary>
    /// <remarks>
    /// Prefer <see cref="UpdateInboxMessage(string, Func{InboxMessage, InboxMessage})"/> when the
    /// new value depends on the stored one: this overload overwrites whatever is there.
    /// </remarks>
    /// <param name="message">The updated message; matched to the stored one by <see cref="InboxMessage.MessageId"/>.</param>
    /// <exception cref="KeyNotFoundException">No stored message has that identifier.</exception>
    public void UpdateInboxMessage(InboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            if (!_messages.ContainsKey(message.MessageId))
            {
                throw new KeyNotFoundException($"No inbox message has the identifier '{message.MessageId}'.");
            }

            _messages[message.MessageId] = message;
        }
    }

    /// <summary>
    /// Changes a stored inbox message in one atomic step, so a delivery report and a patient
    /// response arriving together cannot overwrite each other.
    /// </summary>
    /// <param name="messageId">Simulator-local identifier of the message.</param>
    /// <param name="change">Produces the new message from the stored one. Runs under the store's lock, so it must be quick and must not call back into the store.</param>
    /// <returns>The message as stored after the change.</returns>
    /// <exception cref="KeyNotFoundException">No stored message has that identifier.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="change"/> returned a message with a different identifier.</exception>
    public InboxMessage UpdateInboxMessage(string messageId, Func<InboxMessage, InboxMessage> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_gate)
        {
            if (!_messages.TryGetValue(messageId, out var current))
            {
                throw new KeyNotFoundException($"No inbox message has the identifier '{messageId}'.");
            }

            var updated = change(current);
            if (!string.Equals(updated.MessageId, messageId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("An update must not change the message identifier.");
            }

            _messages[messageId] = updated;
            return updated;
        }
    }

    /// <summary>
    /// Appends one exchange to the FHIR message log.
    /// </summary>
    /// <remarks>The log keeps the newest <see cref="LogCapacity"/> entries.</remarks>
    /// <param name="entry">The exchange to record.</param>
    public void AppendLogEntry(FhirMessageLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _log.Enqueue(entry);
            while (_log.Count > LogCapacity)
            {
                _log.Dequeue();
            }
        }
    }

    /// <summary>
    /// Lists the FHIR message log.
    /// </summary>
    /// <returns>A snapshot of the logged exchanges, newest first.</returns>
    public IReadOnlyList<FhirMessageLogEntry> GetLogEntries()
    {
        lock (_gate)
        {
            return [.. _log.Reverse().OrderByDescending(e => e.At)];
        }
    }

    /// <summary>
    /// Finds the behaviour in force for a patient.
    /// </summary>
    /// <param name="patientSyntheticId">Synthetic identifier of the patient.</param>
    /// <returns>The patient's own behaviour, or the default behaviour when none was set for them.</returns>
    public PatientBehaviour GetBehaviour(string patientSyntheticId)
    {
        lock (_gate)
        {
            return _behaviours.TryGetValue(patientSyntheticId, out var assignment) ? assignment.Behaviour : _defaultBehaviour;
        }
    }

    /// <summary>
    /// Finds the behaviour set for one identifier, ignoring the default.
    /// </summary>
    /// <param name="syntheticId">Synthetic identifier of a patient, or a caregiver's inbox address.</param>
    /// <returns>The behaviour set for exactly that identifier, or <see langword="null"/> when there is none.</returns>
    public PatientBehaviour? FindOwnBehaviour(string syntheticId)
    {
        lock (_gate)
        {
            return _behaviours.TryGetValue(syntheticId, out var assignment) ? assignment.Behaviour : null;
        }
    }

    /// <summary>
    /// Lists the patients that have a behaviour of their own.
    /// </summary>
    /// <returns>One assignment per patient, ordered by patient identifier. The default behaviour is not included.</returns>
    public IReadOnlyList<PatientBehaviourAssignment> GetBehaviours()
    {
        lock (_gate)
        {
            return [.. _behaviours.Values.OrderBy(a => a.PatientSyntheticId, StringComparer.Ordinal)];
        }
    }

    /// <summary>
    /// Sets or replaces the behaviour of one patient.
    /// </summary>
    /// <param name="assignment">The patient, their behaviour and the scenario that set it, if any.</param>
    /// <remarks>
    /// Notifications already in the inbox keep the reactions they already had, a response still
    /// waiting for its due time under the previous behaviour is dropped, and the patient's
    /// notification numbers start again: <see cref="PatientBehaviour.RespondToNotification"/> counts
    /// the notifications received under this behaviour. Otherwise a scenario run a second time
    /// would find its patient already past the notification they respond to.
    /// </remarks>
    public void SetBehaviour(PatientBehaviourAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        lock (_gate)
        {
            _behaviours[assignment.PatientSyntheticId] = assignment;
            _behaviourInForceFrom[assignment.PatientSyntheticId] = _messageOrder.Count;
            DropPendingResponses(id => string.Equals(id, assignment.PatientSyntheticId, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Removes a patient's own behaviour, so the default applies to them again.
    /// </summary>
    /// <param name="patientSyntheticId">Synthetic identifier of the patient.</param>
    /// <returns><see langword="true"/> when the patient had a behaviour of their own.</returns>
    public bool RemoveBehaviour(string patientSyntheticId)
    {
        lock (_gate)
        {
            _behaviourInForceFrom.Remove(patientSyntheticId);
            DropPendingResponses(id => string.Equals(id, patientSyntheticId, StringComparison.Ordinal));
            return _behaviours.Remove(patientSyntheticId);
        }
    }

    /// <summary>
    /// Reads the behaviour used for patients that have none of their own.
    /// </summary>
    /// <returns>The default behaviour. Until changed it is deliver and do not respond, so nothing is acknowledged unless a behaviour says so.</returns>
    public PatientBehaviour GetDefaultBehaviour()
    {
        lock (_gate)
        {
            return _defaultBehaviour;
        }
    }

    /// <summary>
    /// Replaces the behaviour used for patients that have none of their own.
    /// </summary>
    /// <remarks>
    /// As with <see cref="SetBehaviour"/>, notification numbers start again for the patients the
    /// default applies to.
    /// </remarks>
    /// <param name="behaviour">The new default.</param>
    public void SetDefaultBehaviour(PatientBehaviour behaviour)
    {
        ArgumentNullException.ThrowIfNull(behaviour);
        lock (_gate)
        {
            _defaultBehaviour = behaviour;
            _defaultInForceFrom = _messageOrder.Count;
            DropPendingResponses(id => !_behaviours.ContainsKey(id));
        }
    }

    /// <summary>
    /// Finds which notification an inbox message is for its patient: the first, the second and so on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what <see cref="PatientBehaviour.RespondToNotification"/> is compared with. The
    /// number counts the notifications about the same case that the patient received since the
    /// behaviour in force for them was set, up to and including this one. Counting per case
    /// matters: a reminder is the second notification about a case, but the first notification
    /// about the patient's next case is a first notification again.
    /// </para>
    /// <para>
    /// Only messages addressed to the patient count. A caregiver's copy is not a notification the
    /// patient received, so it never moves the patient towards the notification they respond to.
    /// </para>
    /// </remarks>
    /// <param name="messageId">Simulator-local identifier of the inbox message.</param>
    /// <returns>The number, counting from 1; 0 when the message is unknown, is a caregiver's copy, or arrived before the behaviour in force was set.</returns>
    public int GetNotificationNumber(string messageId)
    {
        lock (_gate)
        {
            if (!_messages.TryGetValue(messageId, out var message) || message.RecipientKind != RecipientKind.Patient)
            {
                return 0;
            }

            var from = _behaviourInForceFrom.TryGetValue(message.PatientSyntheticId, out var own) ? own : _defaultInForceFrom;
            var number = 0;
            for (var i = from; i < _messageOrder.Count; i++)
            {
                var other = _messages[_messageOrder[i]];
                if (other.RecipientKind == RecipientKind.Patient
                    && string.Equals(other.PatientSyntheticId, message.PatientSyntheticId, StringComparison.Ordinal)
                    && string.Equals(other.CaseTaskId, message.CaseTaskId, StringComparison.Ordinal))
                {
                    number++;
                }

                if (ReferenceEquals(other, message))
                {
                    return number;
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// Records one reaction to a notification.
    /// </summary>
    /// <param name="reaction">The reaction to record.</param>
    public void AppendReaction(PatientReactionRecord reaction)
    {
        ArgumentNullException.ThrowIfNull(reaction);
        lock (_gate)
        {
            _reactions.Add(reaction);
        }
    }

    /// <summary>
    /// Lists the reactions recorded for a set of patients.
    /// </summary>
    /// <param name="patientSyntheticIds">The patients to include.</param>
    /// <returns>Their reactions, oldest first.</returns>
    public IReadOnlyList<PatientReactionRecord> GetReactions(IReadOnlyCollection<string> patientSyntheticIds)
    {
        ArgumentNullException.ThrowIfNull(patientSyntheticIds);
        var wanted = patientSyntheticIds.ToHashSet(StringComparer.Ordinal);
        lock (_gate)
        {
            // Stable sort, so reactions stamped with the same instant keep the order they happened in.
            return [.. _reactions.Where(r => wanted.Contains(r.PatientSyntheticId)).OrderBy(r => r.At)];
        }
    }

    /// <summary>
    /// Stores a scenario run, replacing any earlier state with the same run identifier.
    /// </summary>
    /// <param name="run">The run state to store.</param>
    public void SaveRun(ScenarioRunState run)
    {
        ArgumentNullException.ThrowIfNull(run);
        lock (_gate)
        {
            _runs[run.RunId] = run;
        }
    }

    /// <summary>
    /// Looks up a scenario run.
    /// </summary>
    /// <param name="runId">Simulator-local identifier of the run.</param>
    /// <returns>The run state, or <see langword="null"/> when no run has that identifier.</returns>
    public ScenarioRunState? FindRun(string runId)
    {
        lock (_gate)
        {
            return _runs.GetValueOrDefault(runId);
        }
    }

    /// <summary>
    /// Queues a patient response that is due later.
    /// </summary>
    /// <param name="response">The response and the time it is due.</param>
    public void SchedulePendingResponse(PendingResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        lock (_gate)
        {
            _pending.Add(response);
        }
    }

    /// <summary>
    /// Takes the queued responses that are due, removing them from the queue.
    /// </summary>
    /// <remarks>
    /// Taking and removing are one atomic step, so a response is handed out exactly once even if
    /// two dispatchers ask at the same moment.
    /// </remarks>
    /// <param name="now">The current time on the simulator clock.</param>
    /// <returns>The responses whose due time is at or before <paramref name="now"/>, earliest first.</returns>
    public IReadOnlyList<PendingResponse> ClaimDueResponses(DateTimeOffset now)
    {
        lock (_gate)
        {
            var due = _pending.Where(p => p.DueAt <= now).OrderBy(p => p.DueAt).ToList();
            _pending.RemoveAll(p => p.DueAt <= now);
            return due;
        }
    }

    /// <summary>
    /// Counts the responses still waiting for their due time.
    /// </summary>
    /// <returns>The number of queued responses.</returns>
    public int CountPendingResponses()
    {
        lock (_gate)
        {
            return _pending.Count;
        }
    }

    /// <summary>
    /// Queues a delivery report to be tried again later, unless one is already queued for the message.
    /// </summary>
    /// <param name="report">The message and the time the retry is due.</param>
    /// <returns><see langword="true"/> when the retry was queued; <see langword="false"/> when the message already has one waiting.</returns>
    public bool ScheduleDeliveryReport(PendingDeliveryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        lock (_gate)
        {
            if (_deliveryReports.Exists(r => string.Equals(r.MessageId, report.MessageId, StringComparison.Ordinal)))
            {
                return false;
            }

            _deliveryReports.Add(report);
            return true;
        }
    }

    /// <summary>
    /// Takes the queued delivery reports that are due, removing them from the queue.
    /// </summary>
    /// <param name="now">The current time on the simulator clock.</param>
    /// <returns>The reports whose due time is at or before <paramref name="now"/>, earliest first.</returns>
    public IReadOnlyList<PendingDeliveryReport> ClaimDueDeliveryReports(DateTimeOffset now)
    {
        lock (_gate)
        {
            var due = _deliveryReports.Where(r => r.DueAt <= now).OrderBy(r => r.DueAt).ToList();
            _deliveryReports.RemoveAll(r => r.DueAt <= now);
            return due;
        }
    }

    // A response that is still waiting was decided by the behaviour that has just been replaced.
    // Sending it anyway would make a patient who was just set to stay silent respond.
    private void DropPendingResponses(Func<string, bool> forPatient) =>
        _pending.RemoveAll(p => _messages.TryGetValue(p.MessageId, out var message) && forPatient(message.PatientSyntheticId));

    private void Store(InboxMessage message)
    {
        _messages[message.MessageId] = message;
        _messageIdByRequest[message.CommunicationRequestId] = message.MessageId;
        _messageOrder.Add(message.MessageId);
    }
}
