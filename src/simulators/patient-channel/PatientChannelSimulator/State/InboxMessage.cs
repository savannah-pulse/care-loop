namespace PatientChannelSimulator.State;

/// <summary>
/// One simulated message in the simulated patient inbox, created from an inbound FHIR
/// <c>CommunicationRequest</c>.
/// </summary>
/// <remarks>
/// Holds only what the patient channel was sent: identifiers, references and rendered template
/// text. It is synthetic data and <paramref name="Simulated"/> is always <see langword="true"/>
/// so every surface that shows it can label it as simulated.
/// </remarks>
/// <param name="MessageId">Simulator-local identifier of the inbox message, used in the inbox routes.</param>
/// <param name="CommunicationRequestId">Logical id of the FHIR <c>CommunicationRequest</c> that produced the message; outbound <c>Communication</c>s point back to it through <c>basedOn</c>.</param>
/// <param name="CaseTaskId">Logical id of the CareLoop case <c>Task</c> the notification belongs to, used to read case status for the patient view; <see langword="null"/> when the request did not reference one.</param>
/// <param name="PatientSyntheticId">Synthetic identifier of whoever the message is for (always prefixed <c>SYN-</c>): the patient, or for a caregiver's copy the caregiver's inbox address, because CareLoop does not name the patient on those.</param>
/// <param name="PatientDisplayName">Visibly synthetic display name (always starts with "Synthetic").</param>
/// <param name="Channel">The simulated channel the message was presented on.</param>
/// <param name="Language">BCP 47 language tag of the rendered content, as sent by CareLoop.</param>
/// <param name="Body">The message text exactly as CareLoop rendered it; the simulator never rewrites content.</param>
/// <param name="ReceivedAt">When the simulator received the request, taken from the simulator clock.</param>
/// <param name="Delivery">What has been reported to CareLoop about delivery.</param>
/// <param name="DeliveryFailureReason">Reason text reported with a failed delivery; <see langword="null"/> unless <paramref name="Delivery"/> is <see cref="DeliveryState.Failed"/>.</param>
/// <param name="Response">The patient's latest response.</param>
/// <param name="RespondedAt">When the patient last responded; <see langword="null"/> while <paramref name="Response"/> is <see cref="ResponseState.None"/>.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
/// <param name="RecipientKind">Whether the message is for the patient or is a caregiver's copy. A caregiver's copy can never be answered.</param>
/// <param name="Confidence">The patient's latest 1 to 5 confidence rating (C10); <see langword="null"/> until they rate. A rating is a measure, not an acknowledgement, so it never changes <paramref name="Response"/>.</param>
/// <param name="ConfidenceRatedAt">When the patient last rated; <see langword="null"/> while <paramref name="Confidence"/> is.</param>
/// <param name="CallRequestCount">How many call-me requests CareLoop has accepted for this message; gives each further request its own version.</param>
/// <param name="ConfidenceRatingCount">How many confidence ratings CareLoop has accepted for this message; gives each further rating its own version.</param>
public sealed record InboxMessage(
    string MessageId,
    string CommunicationRequestId,
    string? CaseTaskId,
    string PatientSyntheticId,
    string PatientDisplayName,
    InboxChannel Channel,
    string Language,
    string Body,
    DateTimeOffset ReceivedAt,
    DeliveryState Delivery,
    string? DeliveryFailureReason,
    ResponseState Response,
    DateTimeOffset? RespondedAt,
    bool Simulated = true,
    RecipientKind RecipientKind = RecipientKind.Patient,
    int? Confidence = null,
    DateTimeOffset? ConfidenceRatedAt = null,
    int CallRequestCount = 0,
    int ConfidenceRatingCount = 0);
