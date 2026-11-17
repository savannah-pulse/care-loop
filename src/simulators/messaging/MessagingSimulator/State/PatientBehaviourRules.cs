namespace MessagingSimulator.State;

/// <summary>
/// The rules a <see cref="PatientBehaviour"/> must meet before it is put in force.
/// </summary>
/// <remarks>
/// Shared by the patient-behaviour routes and the scenario runner, so a behaviour set by hand and
/// one read from a scenario file are held to the same rules.
/// </remarks>
public static class PatientBehaviourRules
{
    /// <summary>Prefix every synthetic patient identifier carries.</summary>
    public const string SyntheticPrefix = "SYN-";

    /// <summary>
    /// The longest delay a behaviour may wait before responding. Far longer than any pathway
    /// deadline, and short enough that a due time can always be computed.
    /// </summary>
    public static readonly TimeSpan MaximumRespondAfter = TimeSpan.FromDays(365);

    /// <summary>
    /// Checks a behaviour.
    /// </summary>
    /// <param name="behaviour">The behaviour to check.</param>
    /// <returns>Why the behaviour is not allowed, or <see langword="null"/> when it is.</returns>
    public static string? Check(PatientBehaviour behaviour)
    {
        ArgumentNullException.ThrowIfNull(behaviour);

        if (!Enum.IsDefined(behaviour.Delivery) || !Enum.IsDefined(behaviour.Response))
        {
            return "The delivery or response value is not a known one.";
        }

        if (behaviour.Delivery == DeliveryBehaviour.Fail && string.IsNullOrWhiteSpace(behaviour.FailureReason))
        {
            return "A delivery that fails needs a failure reason.";
        }

        if (behaviour.Delivery != DeliveryBehaviour.Deliver && behaviour.Response != PatientResponseBehaviour.None)
        {
            return "A patient can only respond to a delivered notification: the response must be None unless delivery is Deliver.";
        }

        if (behaviour.RespondAfter < TimeSpan.Zero)
        {
            return "RespondAfter cannot be negative.";
        }

        if (behaviour.RespondAfter > MaximumRespondAfter)
        {
            return $"RespondAfter cannot be longer than {MaximumRespondAfter.TotalDays:0} days.";
        }

        if (behaviour.RespondToNotification < 1)
        {
            return "RespondToNotification counts from 1.";
        }

        return null;
    }

    /// <summary>
    /// Checks a patient identifier or the address of a message.
    /// </summary>
    /// <param name="patientSyntheticId">The identifier to check.</param>
    /// <returns><see langword="true"/> when it is visibly synthetic (prefixed <c>SYN-</c>).</returns>
    public static bool IsSynthetic(string patientSyntheticId) =>
        patientSyntheticId is not null
        && patientSyntheticId.Length > SyntheticPrefix.Length
        && patientSyntheticId.StartsWith(SyntheticPrefix, StringComparison.Ordinal);
}
