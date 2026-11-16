using Hl7.Fhir.Utility;

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
    /// <exception cref="FormatException">CareLoop's answer was not a valid FHIR <c>Task</c>.</exception>
    public async Task<PatientCaseStatus?> ReadAsync(string taskId, CancellationToken ct)
    {
        var task = await _careLoop.GetTaskAsync(taskId, ct);
        if (task is null)
        {
            return null;
        }

        // Only coordination fields are copied. Owner, requester, focus, input and output stay behind.
        return new PatientCaseStatus(
            TaskId: task.Id ?? taskId,
            TaskStatus: task.Status?.GetLiteral() ?? "unknown",
            Stage: FirstNonBlank(task.BusinessStatus?.Coding.Select(c => c.Code), task.BusinessStatus?.Text),
            StatusReason: FirstNonBlank([task.StatusReason?.Text], FirstNonBlank(task.StatusReason?.Coding.Select(c => c.Display ?? c.Code), null)),
            DueBy: task.Restriction?.Period?.EndElement?.ToDateTimeOffset(TimeSpan.Zero),
            LastUpdated: task.LastModifiedElement?.ToDateTimeOffset(TimeSpan.Zero) ?? task.Meta?.LastUpdated);
    }

    private static string? FirstNonBlank(IEnumerable<string?>? candidates, string? fallback) =>
        candidates?.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c)) ?? (string.IsNullOrWhiteSpace(fallback) ? null : fallback);
}
