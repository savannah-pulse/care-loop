namespace CareLoop.Api.Worker;

/// <summary>
/// Settings for the deadline worker, bound to the <c>DeadlineWorker</c> configuration section.
/// </summary>
/// <remarks>These are technical polling settings only. Clinical deadlines come from the pathway definition.</remarks>
public sealed class DeadlineWorkerOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "DeadlineWorker";

    /// <summary>Whether the worker background service runs. Test hosts may leave it off and sweep explicitly.</summary>
    public bool Enabled { get; set; }

    /// <summary>How often the worker checks open cases against their deadlines.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);
}
