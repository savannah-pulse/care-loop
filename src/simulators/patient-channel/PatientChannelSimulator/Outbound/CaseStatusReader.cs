namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Reads a case's status from CareLoop for the patient case-status view.
/// </summary>
/// <remarks>
/// Read-only. It maps the case <c>Task</c> into <see cref="PatientCaseStatus"/> and exposes only
/// coordination fields; it must not surface owner identities or clinical detail to the patient view.
/// </remarks>
public sealed class CaseStatusReader
{
    private readonly CareLoopFhirClient _careLoop;

    /// <summary>
    /// Creates the reader.
    /// </summary>
    /// <param name="careLoop">Fetches the case <c>Task</c> from CareLoop.</param>
    public CaseStatusReader(CareLoopFhirClient careLoop)
    {
        _careLoop = careLoop;
    }

    /// <summary>
    /// Fetches and maps the status of one case.
    /// </summary>
    /// <param name="taskId">Logical id of the case <c>Task</c> in CareLoop.</param>
    /// <param name="ct">Cancels the call.</param>
    /// <returns>The patient-facing status, or <see langword="null"/> when CareLoop has no such task.</returns>
    /// <exception cref="HttpRequestException">CareLoop could not be reached.</exception>
    public Task<PatientCaseStatus?> ReadAsync(string taskId, CancellationToken ct)
    {
        // TODO(WS-8): call CareLoopFhirClient.GetTaskAsync and map status, businessStatus, statusReason,
        // restriction.period.end and lastModified into PatientCaseStatus.
        _ = _careLoop;
        throw new NotImplementedException("WS8: CaseStatusReader.ReadAsync");
    }
}
