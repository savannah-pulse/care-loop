using CareLoop.Domain.Time;

namespace CareLoop.Api.Infrastructure.Time;

/// <summary>
/// The production clock: reports the real current time.
/// </summary>
/// <remarks>This is the only class in CareLoop allowed to read the system time.</remarks>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
