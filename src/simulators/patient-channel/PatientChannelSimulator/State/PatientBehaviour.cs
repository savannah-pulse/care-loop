namespace PatientChannelSimulator.State;

/// <summary>
/// How one simulated patient reacts when CareLoop notifies them.
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
/// ClientApp. A patient with none gets the simulator's default behaviour.
/// </para>
/// </remarks>
/// <param name="Delivery">What the channel reports for each notification the patient receives.</param>
/// <param name="Response">What the patient does after a delivered notification. Must be <see cref="PatientResponseBehaviour.None"/> unless <paramref name="Delivery"/> is <see cref="DeliveryBehaviour.Deliver"/>.</param>
/// <param name="FailureReason">Why delivery failed; required when <paramref name="Delivery"/> is <see cref="DeliveryBehaviour.Fail"/>.</param>
/// <param name="RespondAfter">How long after delivery the patient responds, in real time. A scenario run has already compressed the scenario's delay by its pacing.</param>
/// <param name="RespondToNotification">
/// Which notification the patient responds to, counting the notifications this patient has
/// received: 1 is the first, 2 describes a patient who only reacts to a reminder. Earlier
/// notifications get the delivery behaviour and no response.
/// </param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
/// <param name="Confidence">
/// The 1 to 5 confidence rating the patient gives (C10). Required when <paramref name="Response"/>
/// is <see cref="PatientResponseBehaviour.RateConfidence"/>; with any other response the patient
/// rates straight after responding; must be <see langword="null"/> when the patient does not respond.
/// </param>
public sealed record PatientBehaviour(
    DeliveryBehaviour Delivery,
    PatientResponseBehaviour Response,
    string? FailureReason = null,
    TimeSpan RespondAfter = default,
    int RespondToNotification = 1,
    bool Simulated = true,
    int? Confidence = null);
