namespace MessagingSimulator.Scenarios;

/// <summary>
/// A synthetic staff member performing a step (<c>$defs/staff</c> in the schema).
/// </summary>
public sealed record ScenarioStaff
{
    /// <summary>Synthetic staff identifier; always prefixed <c>SYN-</c>.</summary>
    public required string SyntheticId { get; init; }

    /// <summary>Demo role key, for example <c>clinician</c>, <c>coordinator</c> or <c>facility-lead</c>.</summary>
    public required string Role { get; init; }

    /// <summary>Synthetic facility identifier (prefixed <c>SYN-</c>), when the step names one.</summary>
    public string? Facility { get; init; }
}
