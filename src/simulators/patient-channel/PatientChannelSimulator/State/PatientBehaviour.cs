namespace PatientChannelSimulator.State;

/// <summary>
/// How one simulated patient reacts when CareLoop sends them a text message or an email.
/// </summary>
/// <remarks>
/// <para>
/// A behaviour is a reaction, not a timetable. It is applied when a notification for the patient
/// actually arrives, so a test that expects an acknowledgement fails if CareLoop never sent the
/// notification. Each patient has their own behaviour, which is what lets a cohort contain
/// patients who behave differently.
/// </para>
/// <para>
/// Behaviours come from the <c>behaviour</c> of a scenario patient, or are set by hand in the
/// ClientApp. A patient with none gets the simulator's default behaviour. A behaviour is found by
/// the address a message was sent to; until scenario files carry phone numbers and email addresses,
/// the hospital system simulator sends a patient's synthetic identifier as that address.
/// </para>
/// </remarks>
public sealed record PatientBehaviour
{
    /// <summary>
    /// Creates a behaviour with every optional value at its default; used when reading JSON.
    /// </summary>
    public PatientBehaviour()
    {
    }

    /// <summary>
    /// Creates a behaviour.
    /// </summary>
    /// <param name="delivery">What the channel reports for each notification the patient receives.</param>
    /// <param name="response">What the patient does after a delivered notification.</param>
    /// <param name="failureReason">Why delivery failed.</param>
    /// <param name="respondAfter">How long after delivery the patient responds, in real time.</param>
    /// <param name="respondToNotification">Which notification the patient responds to, counting from 1.</param>
    /// <param name="simulated">Visible simulated-data marker.</param>
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public PatientBehaviour(
        DeliveryBehaviour delivery,
        PatientResponseBehaviour response,
        string? failureReason = null,
        TimeSpan respondAfter = default,
        int respondToNotification = 1,
        bool simulated = true)
    {
        Delivery = delivery;
        Response = response;
        FailureReason = failureReason;
        RespondAfter = respondAfter;
        RespondToNotification = respondToNotification;
        Simulated = simulated;
    }

    /// <summary>What the channel reports for each notification the patient receives.</summary>
    public required DeliveryBehaviour Delivery { get; init; }

    /// <summary>What the patient does after a delivered notification. Must be <see cref="PatientResponseBehaviour.None"/> unless <see cref="Delivery"/> is <see cref="DeliveryBehaviour.Deliver"/>.</summary>
    public required PatientResponseBehaviour Response { get; init; }

    /// <summary>Why delivery failed; required when <see cref="Delivery"/> is <see cref="DeliveryBehaviour.Fail"/>.</summary>
    public string? FailureReason { get; init; }

    /// <summary>How long after delivery the patient responds, in real time. A scenario run has already compressed the scenario's delay by its pacing.</summary>
    public TimeSpan RespondAfter { get; init; }

    /// <summary>
    /// Which notification the patient responds to, counting the notifications about one case
    /// that the patient received since this behaviour was set: 1 is the first, 2 describes a
    /// patient who only reacts to a reminder. Other notifications about that case get the
    /// delivery behaviour and no response.
    /// </summary>
    public int RespondToNotification { get; init; } = 1;

    /// <summary>Visible simulated-data marker; always <see langword="true"/>.</summary>
    public bool Simulated { get; init; } = true;

}
