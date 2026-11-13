namespace CareLoop.Domain.Events;

/// <summary>
/// What CareLoop did with a source event.
/// </summary>
public enum SourceEventDisposition
{
    /// <summary>The event passed its guards and changed the case projection.</summary>
    Applied = 0,

    /// <summary>The event was valid but a domain rule refused it (for example closure without evidence). The case is unchanged and the attempt is audited.</summary>
    Rejected = 1,
}
