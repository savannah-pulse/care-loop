namespace CareLoop.Api.Features.Events;

/// <summary>
/// What the event ingestor did with an inbound event.
/// </summary>
public enum IngestOutcome
{
    /// <summary>The event was stored and changed the case.</summary>
    Applied = 0,

    /// <summary>The same source event had already been stored; nothing changed.</summary>
    Duplicate = 1,

    /// <summary>The event was stored and audited but a domain rule refused it, so the case is unchanged.</summary>
    Rejected = 2,

    /// <summary>The envelope was malformed or referred to an unknown case; nothing was stored.</summary>
    Invalid = 3,
}
