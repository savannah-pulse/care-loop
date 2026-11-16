using System.Globalization;
using Hl7.Fhir.Model;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Builds the FHIR <c>Communication</c> sent when the patient rates their confidence from 1 to 5 (C10).
/// </summary>
/// <remarks>
/// Status <c>completed</c>, category <see cref="CommunicationCodes.ConfidenceRating"/>, sender the patient,
/// with the rating as an integer in the first <c>payload.contentString</c>.
/// A rating is a measure, not an acknowledgement: it has its own category so CareLoop can never
/// read it as one, and it leaves the notification track and the deadlines as they were.
/// Every built resource is tagged as simulated (<see cref="CommunicationCodes.SimulatedTagCode"/>),
/// points back to the originating request through <c>basedOn</c> and to the case <c>Task</c> through
/// <c>about</c>, and has a deterministic id and <c>meta.versionId</c> so CareLoop deduplicates a
/// resend of the same event.
/// </remarks>
public sealed class ConfidenceRatingCommunicationBuilder
{
    /// <summary>The lowest rating a patient can give.</summary>
    public const int MinimumRating = 1;

    /// <summary>The highest rating a patient can give.</summary>
    public const int MaximumRating = 5;

    /// <summary>
    /// Builds the resource for one inbox message.
    /// </summary>
    /// <param name="message">The inbox message the event is about; supplies the request, patient and case references.</param>
    /// <param name="occurredAt">When the event happened; becomes <c>sent</c> on the resource.</param>
    /// <param name="rating">The patient's rating, from <see cref="MinimumRating"/> to <see cref="MaximumRating"/>.</param>
    /// <returns>A FHIR R4 <c>Communication</c> ready to send to CareLoop.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rating"/> is outside 1 to 5.</exception>
    public Communication Build(InboxMessage message, DateTimeOffset occurredAt, int rating)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(rating, MinimumRating);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rating, MaximumRating);

        // A patient may rate again. Each rating is its own event, so it gets the next version;
        // a resend of the same rating reuses the version and is deduplicated.
        var communication = CommunicationFactory.PatientResponse(
            message,
            occurredAt,
            CommunicationCodes.ConfidenceRating,
            CommunicationFactory.ConfidenceRatingSuffix,
            version: message.ConfidenceRatingCount + 1);
        communication.Payload =
        [
            new Communication.PayloadComponent { Content = new FhirString(rating.ToString(CultureInfo.InvariantCulture)) },
        ];
        return communication;
    }
}
