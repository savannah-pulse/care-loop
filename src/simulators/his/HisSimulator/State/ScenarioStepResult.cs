namespace HisSimulator.State;

/// <summary>
/// What happened to one scenario step during a run.
/// </summary>
/// <param name="StepId">The step's stable identifier from the scenario file.</param>
/// <param name="Status">Current progress of the step.</param>
/// <param name="ScheduledAt">When the step is due: run start plus the step's <c>at</c> offset compressed by the run's pacing. A real time, not a simulated one.</param>
/// <param name="SourceEventKey">
/// Deterministic key (run, step) the FHIR resource identifiers are derived from, so a resend is deduplicated by CareLoop.
/// </param>
/// <param name="HttpStatusCodes">HTTP status returned by CareLoop for each resource sent by the step, in send order.</param>
/// <param name="LogEntryIds">Identifiers of the <see cref="FhirMessageLogEntry"/> records produced by the step.</param>
/// <param name="Error">Failure description when <paramref name="Status"/> is <see cref="ScenarioStepStatus.Failed"/>; otherwise <see langword="null"/>.</param>
public sealed record ScenarioStepResult(
    string StepId,
    ScenarioStepStatus Status,
    DateTimeOffset ScheduledAt,
    string SourceEventKey,
    IReadOnlyList<int> HttpStatusCodes,
    IReadOnlyList<Guid> LogEntryIds,
    string? Error);
