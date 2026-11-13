namespace CareLoop.Domain.Time;

/// <summary>
/// The single source of "now" for CareLoop.
/// </summary>
/// <remarks>
/// Deadlines, escalation and overdue flags all depend on the current time. Reading the system
/// clock directly would make those rules untestable and would stop the simulation replay from
/// compressing days into seconds. Every component therefore asks an <see cref="IClock"/>, so
/// tests and the simulation can control time. Never call <c>DateTimeOffset.UtcNow</c> directly.
/// </remarks>
public interface IClock
{
    /// <summary>
    /// The current instant in UTC.
    /// </summary>
    DateTimeOffset UtcNow { get; }
}
