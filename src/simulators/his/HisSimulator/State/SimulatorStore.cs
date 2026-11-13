namespace HisSimulator.State;

/// <summary>
/// In-memory state of the HIS simulator: staff inbox, FHIR message log, simulated cases and scenario runs.
/// </summary>
/// <remarks>
/// Registered as a singleton and shared by request handlers and background scenario runs, so the
/// implementation must be thread-safe. Nothing is persisted: restarting the simulator clears all
/// state, which is intended for a tool that only ever holds synthetic data.
/// </remarks>
public sealed class SimulatorStore
{
    /// <summary>
    /// Adds an item to the staff inbox.
    /// </summary>
    /// <param name="item">The item to add; its <see cref="StaffInboxItem.Id"/> must be unique.</param>
    /// <exception cref="ArgumentException">An item with the same identifier already exists.</exception>
    public void AddInboxItem(StaffInboxItem item)
    {
        // TODO(WS-7): store the item in a thread-safe collection keyed by Id; reject duplicate ids.
        throw new NotImplementedException("WS7: SimulatorStore.AddInboxItem");
    }

    /// <summary>
    /// Lists staff inbox items, newest first.
    /// </summary>
    /// <param name="recipientSyntheticId">
    /// When supplied, only items addressed to this synthetic staff identifier are returned.
    /// </param>
    /// <returns>A snapshot of the matching items ordered by <see cref="StaffInboxItem.ReceivedAt"/> descending.</returns>
    public IReadOnlyList<StaffInboxItem> GetInboxItems(string? recipientSyntheticId = null)
    {
        // TODO(WS-7): return an immutable snapshot, filtered by recipient when given, newest first.
        throw new NotImplementedException("WS7: SimulatorStore.GetInboxItems");
    }

    /// <summary>
    /// Finds one staff inbox item.
    /// </summary>
    /// <param name="id">Identifier of the item.</param>
    /// <returns>The item, or <see langword="null"/> if none has that identifier.</returns>
    public StaffInboxItem? GetInboxItem(Guid id)
    {
        // TODO(WS-7): look the item up by id.
        throw new NotImplementedException("WS7: SimulatorStore.GetInboxItem");
    }

    /// <summary>
    /// Appends an entry to the FHIR message log.
    /// </summary>
    /// <param name="entry">The sent or received message to record.</param>
    public void AppendLogEntry(FhirMessageLogEntry entry)
    {
        // TODO(WS-7): append in arrival order; consider a size cap so a long cohort run cannot exhaust memory.
        throw new NotImplementedException("WS7: SimulatorStore.AppendLogEntry");
    }

    /// <summary>
    /// Lists FHIR message log entries, newest first.
    /// </summary>
    /// <param name="direction">When supplied, only entries travelling in this direction are returned.</param>
    /// <param name="caseRef">When supplied, only entries correlated to this simulator-side case handle are returned.</param>
    /// <returns>A snapshot of the matching entries ordered by <see cref="FhirMessageLogEntry.At"/> descending.</returns>
    public IReadOnlyList<FhirMessageLogEntry> GetLogEntries(FhirMessageDirection? direction = null, string? caseRef = null)
    {
        // TODO(WS-7): return an immutable snapshot with both optional filters applied, newest first.
        throw new NotImplementedException("WS7: SimulatorStore.GetLogEntries");
    }

    /// <summary>
    /// Inserts or replaces the simulated HIS record of a case.
    /// </summary>
    /// <param name="case">The case to store, keyed by <see cref="SimulatedCase.CaseRef"/>.</param>
    public void UpsertCase(SimulatedCase @case)
    {
        // TODO(WS-7): add or replace by CaseRef.
        throw new NotImplementedException("WS7: SimulatorStore.UpsertCase");
    }

    /// <summary>
    /// Finds the simulated HIS record of a case.
    /// </summary>
    /// <param name="caseRef">Simulator-side handle of the case.</param>
    /// <returns>The case, or <see langword="null"/> if the simulator has not sent a screening result for it.</returns>
    public SimulatedCase? GetCase(string caseRef)
    {
        // TODO(WS-7): look the case up by CaseRef.
        throw new NotImplementedException("WS7: SimulatorStore.GetCase");
    }

    /// <summary>
    /// Lists every simulated case the HIS knows about.
    /// </summary>
    /// <returns>A snapshot of all cases, in creation order.</returns>
    public IReadOnlyList<SimulatedCase> GetCases()
    {
        // TODO(WS-7): return an immutable snapshot in creation order.
        throw new NotImplementedException("WS7: SimulatorStore.GetCases");
    }

    /// <summary>
    /// Inserts or replaces the state of a scenario run.
    /// </summary>
    /// <param name="run">The run state to store, keyed by <see cref="ScenarioRun.RunId"/>.</param>
    public void SaveRun(ScenarioRun run)
    {
        // TODO(WS-7): add or replace by RunId so progress updates are visible to GET /scenarios/runs/{runId}.
        throw new NotImplementedException("WS7: SimulatorStore.SaveRun");
    }

    /// <summary>
    /// Finds the state of a scenario run.
    /// </summary>
    /// <param name="runId">Identifier of the run.</param>
    /// <returns>The latest saved state, or <see langword="null"/> if no run has that identifier.</returns>
    public ScenarioRun? GetRun(Guid runId)
    {
        // TODO(WS-7): look the run up by RunId.
        throw new NotImplementedException("WS7: SimulatorStore.GetRun");
    }

    /// <summary>
    /// Clears every inbox item, log entry, case and run.
    /// </summary>
    /// <remarks>
    /// Lets a demo or an end-to-end test start from a clean simulator without restarting the process.
    /// </remarks>
    public void Reset()
    {
        // TODO(WS-7): clear all collections atomically.
        throw new NotImplementedException("WS7: SimulatorStore.Reset");
    }
}
