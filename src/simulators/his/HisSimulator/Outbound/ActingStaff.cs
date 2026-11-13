namespace HisSimulator.Outbound;

/// <summary>
/// The synthetic member of staff on whose behalf the simulated HIS sends an event.
/// </summary>
/// <remarks>
/// CareLoop guards transitions by who performs them (for example closure needs an authorized human),
/// so every staff-driven event names its performer.
/// </remarks>
/// <param name="SyntheticId">Synthetic staff identifier; always prefixed <c>SYN-</c>.</param>
/// <param name="Role">Demo role key, for example <c>clinician</c>, <c>coordinator</c> or <c>facility-lead</c>.</param>
/// <param name="Facility">Synthetic facility identifier (prefixed <c>SYN-</c>) the person acts for, when known.</param>
public sealed record ActingStaff(string SyntheticId, string Role, string? Facility);
