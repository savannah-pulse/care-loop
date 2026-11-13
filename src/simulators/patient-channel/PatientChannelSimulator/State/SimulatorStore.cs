namespace PatientChannelSimulator.State;

/// <summary>
/// In-memory state of the patient-channel simulator: the simulated patient inbox, the FHIR
/// message log, scenario runs, patient behaviours and the reactions to notifications.
/// </summary>
/// <remarks>
/// Registered as a singleton and therefore shared by concurrent requests and the scenario
/// runner; the implementation must be thread-safe. Everything here is synthetic and is lost on
/// restart by design: the simulator is not a system of record.
/// </remarks>
public sealed class SimulatorStore
{
    /// <summary>
    /// Adds a newly received message to the simulated patient inbox.
    /// </summary>
    /// <param name="message">The message to store; its <see cref="InboxMessage.MessageId"/> must be unique.</param>
    /// <exception cref="InvalidOperationException">A message with the same identifier is already stored.</exception>
    public void AddInboxMessage(InboxMessage message)
    {
        // TODO(WS-8): store the message keyed by MessageId in a thread-safe collection and reject duplicates.
        throw new NotImplementedException("WS8: SimulatorStore.AddInboxMessage");
    }

    /// <summary>
    /// Lists the simulated patient inbox.
    /// </summary>
    /// <returns>A snapshot of all inbox messages, newest first.</returns>
    public IReadOnlyList<InboxMessage> GetInboxMessages()
    {
        // TODO(WS-8): return a snapshot ordered by ReceivedAt descending.
        throw new NotImplementedException("WS8: SimulatorStore.GetInboxMessages");
    }

    /// <summary>
    /// Looks up one inbox message.
    /// </summary>
    /// <param name="messageId">Simulator-local identifier of the message.</param>
    /// <returns>The message, or <see langword="null"/> when no message has that identifier.</returns>
    public InboxMessage? FindInboxMessage(string messageId)
    {
        // TODO(WS-8): look the message up by MessageId.
        throw new NotImplementedException("WS8: SimulatorStore.FindInboxMessage");
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
        // TODO(WS-8): look the message up by CommunicationRequestId.
        throw new NotImplementedException("WS8: SimulatorStore.FindInboxMessageByRequest");
    }

    /// <summary>
    /// Replaces a stored inbox message with an updated copy (for example after a delivery report
    /// or a patient response).
    /// </summary>
    /// <param name="message">The updated message; matched to the stored one by <see cref="InboxMessage.MessageId"/>.</param>
    /// <exception cref="KeyNotFoundException">No stored message has that identifier.</exception>
    public void UpdateInboxMessage(InboxMessage message)
    {
        // TODO(WS-8): atomically replace the stored message with the same MessageId.
        throw new NotImplementedException("WS8: SimulatorStore.UpdateInboxMessage");
    }

    /// <summary>
    /// Appends one exchange to the FHIR message log.
    /// </summary>
    /// <param name="entry">The exchange to record.</param>
    public void AppendLogEntry(FhirMessageLogEntry entry)
    {
        // TODO(WS-8): append to a bounded, thread-safe log (drop the oldest entries beyond a fixed cap).
        throw new NotImplementedException("WS8: SimulatorStore.AppendLogEntry");
    }

    /// <summary>
    /// Lists the FHIR message log.
    /// </summary>
    /// <returns>A snapshot of the logged exchanges, newest first.</returns>
    public IReadOnlyList<FhirMessageLogEntry> GetLogEntries()
    {
        // TODO(WS-8): return a snapshot ordered by At descending.
        throw new NotImplementedException("WS8: SimulatorStore.GetLogEntries");
    }

    /// <summary>
    /// Finds the behaviour in force for a patient.
    /// </summary>
    /// <param name="patientSyntheticId">Synthetic identifier of the patient.</param>
    /// <returns>The patient's own behaviour, or the default behaviour when none was set for them.</returns>
    public PatientBehaviour GetBehaviour(string patientSyntheticId)
    {
        // TODO(WS-8): return the behaviour set for the patient, falling back to GetDefaultBehaviour().
        throw new NotImplementedException("WS8: SimulatorStore.GetBehaviour");
    }

    /// <summary>
    /// Lists the patients that have a behaviour of their own.
    /// </summary>
    /// <returns>One assignment per patient, ordered by patient identifier. The default behaviour is not included.</returns>
    public IReadOnlyList<PatientBehaviourAssignment> GetBehaviours()
    {
        // TODO(WS-8): return a snapshot ordered by PatientSyntheticId.
        throw new NotImplementedException("WS8: SimulatorStore.GetBehaviours");
    }

    /// <summary>
    /// Sets or replaces the behaviour of one patient.
    /// </summary>
    /// <param name="assignment">The patient, their behaviour and the scenario that set it, if any.</param>
    /// <remarks>Notifications already in the inbox keep the reactions they already had.</remarks>
    public void SetBehaviour(PatientBehaviourAssignment assignment)
    {
        // TODO(WS-8): insert or replace atomically, keyed by PatientSyntheticId.
        throw new NotImplementedException("WS8: SimulatorStore.SetBehaviour");
    }

    /// <summary>
    /// Removes a patient's own behaviour, so the default applies to them again.
    /// </summary>
    /// <param name="patientSyntheticId">Synthetic identifier of the patient.</param>
    /// <returns><see langword="true"/> when the patient had a behaviour of their own.</returns>
    public bool RemoveBehaviour(string patientSyntheticId)
    {
        // TODO(WS-8): remove the assignment and report whether one existed.
        throw new NotImplementedException("WS8: SimulatorStore.RemoveBehaviour");
    }

    /// <summary>
    /// Reads the behaviour used for patients that have none of their own.
    /// </summary>
    /// <returns>The default behaviour. Until changed it is deliver and do not respond, so nothing is acknowledged unless a behaviour says so.</returns>
    public PatientBehaviour GetDefaultBehaviour()
    {
        // TODO(WS-8): return the current default, initially Deliver with PatientResponseBehaviour.None.
        throw new NotImplementedException("WS8: SimulatorStore.GetDefaultBehaviour");
    }

    /// <summary>
    /// Replaces the behaviour used for patients that have none of their own.
    /// </summary>
    /// <param name="behaviour">The new default.</param>
    public void SetDefaultBehaviour(PatientBehaviour behaviour)
    {
        // TODO(WS-8): replace the default atomically.
        throw new NotImplementedException("WS8: SimulatorStore.SetDefaultBehaviour");
    }

    /// <summary>
    /// Counts the notifications a patient has received so far.
    /// </summary>
    /// <param name="patientSyntheticId">Synthetic identifier of the patient.</param>
    /// <returns>The number of inbox messages for that patient, which is what <see cref="PatientBehaviour.RespondToNotification"/> is compared with.</returns>
    public int CountNotifications(string patientSyntheticId)
    {
        // TODO(WS-8): count the inbox messages whose PatientSyntheticId matches.
        throw new NotImplementedException("WS8: SimulatorStore.CountNotifications");
    }

    /// <summary>
    /// Records one reaction to a notification.
    /// </summary>
    /// <param name="reaction">The reaction to record.</param>
    public void AppendReaction(PatientReactionRecord reaction)
    {
        // TODO(WS-8): append to a thread-safe list.
        throw new NotImplementedException("WS8: SimulatorStore.AppendReaction");
    }

    /// <summary>
    /// Lists the reactions recorded for a set of patients.
    /// </summary>
    /// <param name="patientSyntheticIds">The patients to include.</param>
    /// <returns>Their reactions, oldest first.</returns>
    public IReadOnlyList<PatientReactionRecord> GetReactions(IReadOnlyCollection<string> patientSyntheticIds)
    {
        // TODO(WS-8): return a snapshot filtered by patient and ordered by At.
        throw new NotImplementedException("WS8: SimulatorStore.GetReactions");
    }

    /// <summary>
    /// Stores a scenario run, replacing any earlier state with the same run identifier.
    /// </summary>
    /// <param name="run">The run state to store.</param>
    public void SaveRun(ScenarioRunState run)
    {
        // TODO(WS-8): insert or replace the run keyed by RunId.
        throw new NotImplementedException("WS8: SimulatorStore.SaveRun");
    }

    /// <summary>
    /// Looks up a scenario run.
    /// </summary>
    /// <param name="runId">Simulator-local identifier of the run.</param>
    /// <returns>The run state, or <see langword="null"/> when no run has that identifier.</returns>
    public ScenarioRunState? FindRun(string runId)
    {
        // TODO(WS-8): look the run up by RunId.
        throw new NotImplementedException("WS8: SimulatorStore.FindRun");
    }
}
