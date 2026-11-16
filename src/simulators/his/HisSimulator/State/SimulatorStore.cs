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
    /// How many FHIR log entries, staff inbox items and scenario runs are kept when no capacity is supplied.
    /// </summary>
    public const int DefaultCapacity = 1000;

    private readonly int _capacity;
    private readonly object _sync = new();
    private readonly Dictionary<Guid, StaffInboxItem> _inbox = [];
    private readonly List<FhirMessageLogEntry> _log = [];
    private readonly Dictionary<string, SimulatedCase> _cases = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, ScenarioRun> _runs = [];
    private readonly Dictionary<string, string> _caseTasks = new(StringComparer.Ordinal);
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    /// <summary>
    /// Creates an empty store.
    /// </summary>
    /// <param name="capacity">
    /// The most FHIR log entries, staff inbox items and scenario runs kept; when exceeded the oldest are dropped, so a
    /// long session cannot grow without limit. Runs that have not finished are never dropped.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than one.</exception>
    public SimulatorStore(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
    }

    /// <summary>
    /// Adds an item to the staff inbox.
    /// </summary>
    /// <param name="item">The item to add; its <see cref="StaffInboxItem.Id"/> must be unique.</param>
    /// <exception cref="ArgumentException">An item with the same identifier already exists.</exception>
    public void AddInboxItem(StaffInboxItem item)
    {
        lock (_sync)
        {
            if (!_inbox.TryAdd(item.Id, item)) throw new ArgumentException("An inbox item with this id already exists.", nameof(item));
            TrimInbox();
        }
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
        lock (_sync) return _inbox.Values.Where(i => recipientSyntheticId is null || i.RecipientSyntheticId == recipientSyntheticId)
            .OrderByDescending(i => i.ReceivedAt).ThenBy(i => i.Id).ToArray();
    }

    /// <summary>
    /// Finds one staff inbox item.
    /// </summary>
    /// <param name="id">Identifier of the item.</param>
    /// <returns>The item, or <see langword="null"/> if none has that identifier.</returns>
    public StaffInboxItem? GetInboxItem(Guid id)
    {
        lock (_sync) return _inbox.GetValueOrDefault(id);
    }

    /// <summary>
    /// Appends an entry to the FHIR message log.
    /// </summary>
    /// <param name="entry">The sent or received message to record.</param>
    public void AppendLogEntry(FhirMessageLogEntry entry)
    {
        lock (_sync)
        {
            _log.Add(entry);
            if (_log.Count > _capacity) _log.RemoveRange(0, _log.Count - _capacity);
        }
    }

    /// <summary>
    /// Lists FHIR message log entries, newest first.
    /// </summary>
    /// <param name="direction">When supplied, only entries travelling in this direction are returned.</param>
    /// <param name="caseRef">When supplied, only entries correlated to this simulator-side case handle are returned.</param>
    /// <param name="since">When supplied, only entries recorded after this instant are returned, so a poller can ask for what is new.</param>
    /// <param name="limit">When supplied, at most this many of the newest matching entries are returned.</param>
    /// <returns>A snapshot of the matching entries ordered by <see cref="FhirMessageLogEntry.At"/> descending.</returns>
    public IReadOnlyList<FhirMessageLogEntry> GetLogEntries(FhirMessageDirection? direction = null, string? caseRef = null,
        DateTimeOffset? since = null, int? limit = null)
    {
        lock (_sync)
        {
            var matches = _log.Where(e => (direction is null || e.Direction == direction) && (caseRef is null || e.CaseRef == caseRef)
                && (since is null || e.At > since)).OrderByDescending(e => e.At);
            return (limit is null ? matches : matches.Take(limit.Value)).ToArray();
        }
    }

    /// <summary>
    /// Finds one FHIR message log entry.
    /// </summary>
    /// <param name="id">Identifier of the entry.</param>
    /// <returns>The entry, or <see langword="null"/> if none has that identifier (it may have been dropped as the oldest).</returns>
    public FhirMessageLogEntry? GetLogEntry(Guid id)
    {
        lock (_sync) return _log.FirstOrDefault(e => e.Id == id);
    }

    /// <summary>
    /// Inserts or replaces the simulated HIS record of a case.
    /// </summary>
    /// <param name="case">The case to store, keyed by <see cref="SimulatedCase.CaseRef"/>.</param>
    public void UpsertCase(SimulatedCase @case)
    {
        lock (_sync)
        {
            var prior = _cases.GetValueOrDefault(@case.CaseRef);
            var ids = new Dictionary<string, string>(prior?.ResourceIds ?? new Dictionary<string, string>());
            foreach (var pair in @case.ResourceIds) ids[pair.Key] = pair.Value;
            _cases[@case.CaseRef] = @case with
            {
                CareLoopTaskId = @case.CareLoopTaskId ?? prior?.CareLoopTaskId ?? _caseTasks.GetValueOrDefault(@case.DiagnosticReportId),
                ResourceIds = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(ids),
            };
        }
    }

    /// <summary>
    /// Finds the simulated HIS record of a case.
    /// </summary>
    /// <param name="caseRef">Simulator-side handle of the case.</param>
    /// <returns>The case, or <see langword="null"/> if the simulator has not sent a screening result for it.</returns>
    public SimulatedCase? GetCase(string caseRef)
    {
        lock (_sync) return _cases.GetValueOrDefault(caseRef);
    }

    /// <summary>
    /// Lists every simulated case the HIS knows about.
    /// </summary>
    /// <returns>A snapshot of all cases, in creation order.</returns>
    public IReadOnlyList<SimulatedCase> GetCases()
    {
        lock (_sync) return _cases.Values.ToArray();
    }

    /// <summary>
    /// Inserts or replaces the state of a scenario run.
    /// </summary>
    /// <param name="run">The run state to store, keyed by <see cref="ScenarioRun.RunId"/>.</param>
    public void SaveRun(ScenarioRun run)
    {
        lock (_sync)
        {
            _runs[run.RunId] = run with { Steps = run.Steps.ToArray() };
            while (_runs.Count > _capacity
                && _runs.Values.Where(r => r.Status is ScenarioRunStatus.Completed or ScenarioRunStatus.Failed && r.RunId != run.RunId)
                    .OrderBy(r => r.StartedAt).FirstOrDefault() is { } oldest)
                _runs.Remove(oldest.RunId);
        }
    }

    /// <summary>
    /// Finds the state of a scenario run.
    /// </summary>
    /// <param name="runId">Identifier of the run.</param>
    /// <returns>The latest saved state, or <see langword="null"/> if no run has that identifier.</returns>
    public ScenarioRun? GetRun(Guid runId)
    {
        lock (_sync) return _runs.GetValueOrDefault(runId);
    }

    /// <summary>
    /// Clears every inbox item, log entry, case and run.
    /// </summary>
    /// <remarks>
    /// Lets a demo or an end-to-end test start from a clean simulator without restarting the process.
    /// </remarks>
    public void Reset()
    {
        lock (_sync) { _inbox.Clear(); _log.Clear(); _cases.Clear(); _runs.Clear(); _caseTasks.Clear(); }
    }

    /// <summary>Replaces the current inbox state on replay of the same FHIR resource.</summary>
    /// <param name="item">The deterministic inbox item.</param>
    internal void UpsertInboxItem(StaffInboxItem item)
    {
        lock (_sync) { _inbox[item.Id] = item; TrimInbox(); }
    }

    private void TrimInbox()
    {
        while (_inbox.Count > _capacity)
            _inbox.Remove(_inbox.Values.OrderBy(i => i.ReceivedAt).First().Id);
    }

    /// <summary>Correlates the API case Task with the source report, including delivery before the initial send finishes.</summary>
    /// <param name="reportId">The source screening report id.</param>
    /// <param name="taskId">The real CareLoop case Task id.</param>
    internal void RecordCaseTask(string reportId, string taskId)
    {
        lock (_sync)
        {
            _caseTasks[reportId] = taskId;
            foreach (var pair in _cases.Where(p => p.Value.DiagnosticReportId == reportId).ToArray())
                _cases[pair.Key] = pair.Value with { CareLoopTaskId = taskId };
        }
    }

    /// <summary>Serializes outbound state changes for one local case across manual actions and runs.</summary>
    /// <param name="caseRef">The case whose FHIR state is being sent.</param>
    /// <returns>The shared gate; callers must release it in a finally block.</returns>
    internal SemaphoreSlim GateFor(string caseRef) => _gates.GetOrAdd(caseRef, _ => new SemaphoreSlim(1));
}
