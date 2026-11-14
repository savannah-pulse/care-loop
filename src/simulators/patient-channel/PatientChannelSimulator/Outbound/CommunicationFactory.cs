using System.Globalization;
using Hl7.Fhir.Model;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Builds the part of a FHIR <c>Communication</c> that every outbound resource has in common.
/// </summary>
/// <remarks>
/// <para>
/// CareLoop's simulator provider accepts a <c>Communication</c> only when <c>about</c> names the
/// case <c>Task</c>, <c>basedOn</c> names the <c>CommunicationRequest</c> it reports on, and
/// <c>sent</c> is present. It deduplicates on the logical id together with <c>meta.versionId</c>.
/// The id is therefore derived from the request id and the kind of event, and the version is
/// stable for a resend, so sending the same event twice is harmless.
/// </para>
/// <para>
/// Every resource carries the simulated-data tag (<see cref="CommunicationCodes.SimulatedTagCode"/>).
/// </para>
/// </remarks>
internal static class CommunicationFactory
{
    /// <summary>Id suffix of a delivery report (delivered or failed: one message has one delivery outcome).</summary>
    internal const string DeliverySuffix = "delivery";

    /// <summary>Id suffix of an acknowledgement.</summary>
    internal const string AcknowledgementSuffix = "acknowledgement";

    /// <summary>Id suffix of a teach-back response.</summary>
    internal const string TeachBackSuffix = "teach-back";

    /// <summary>Id suffix of a call-me request.</summary>
    internal const string CallMeSuffix = "call-me";

    /// <summary>Id suffix of a confidence rating.</summary>
    internal const string ConfidenceRatingSuffix = "confidence-rating";

    /// <summary>
    /// Builds a transport report: sent by the channel about a message for the recipient.
    /// </summary>
    /// <param name="message">The inbox message the report is about.</param>
    /// <param name="occurredAt">When the outcome happened; becomes <c>sent</c>.</param>
    /// <param name="status"><c>completed</c> for delivered, <c>not-done</c> for failed.</param>
    /// <returns>The resource, without a <c>statusReason</c>.</returns>
    internal static Communication TransportReport(InboxMessage message, DateTimeOffset occurredAt, EventStatus status)
    {
        var communication = Create(message, occurredAt, CommunicationCodes.DeliveryStatus, DeliverySuffix, version: 1, status);
        communication.Sender = new ResourceReference
        {
            Identifier = new Identifier(CommunicationCodes.IdentifierSystem, CommunicationCodes.ChannelDeviceIdentifier),
            Display = "Patient-channel simulator (SIMULATED)",
        };
        communication.Recipient = [RecipientOf(message)];
        return communication;
    }

    /// <summary>
    /// Builds a patient response: sent by the patient about a message they received.
    /// </summary>
    /// <param name="message">The inbox message the patient is responding to.</param>
    /// <param name="occurredAt">When the patient responded; becomes <c>sent</c>.</param>
    /// <param name="category">The category code of the response, from <see cref="CommunicationCodes"/>.</param>
    /// <param name="suffix">The id suffix of this kind of response.</param>
    /// <param name="version">The <c>meta.versionId</c>: 1 for a response given once, otherwise the position of this response among those of its kind.</param>
    /// <returns>The resource, with status <c>completed</c>.</returns>
    internal static Communication PatientResponse(InboxMessage message, DateTimeOffset occurredAt, string category, string suffix, int version)
    {
        var communication = Create(message, occurredAt, category, suffix, version, EventStatus.Completed);
        communication.Sender = PatientReference(message);
        return communication;
    }

    /// <summary>
    /// The identity CareLoop deduplicates a <c>Communication</c> on.
    /// </summary>
    /// <param name="communication">A resource built by this factory.</param>
    /// <returns><c>Communication/{id}/_history/{version}</c>.</returns>
    internal static string SourceEventId(Communication communication) =>
        $"Communication/{communication.Id}/_history/{communication.Meta?.VersionId ?? "1"}";

    private static Communication Create(InboxMessage message, DateTimeOffset occurredAt, string category, string suffix, int version, EventStatus status)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);

        var id = $"{message.CommunicationRequestId}-{suffix}";
        var communication = new Communication
        {
            Id = id,
            Meta = new Meta
            {
                VersionId = version.ToString(CultureInfo.InvariantCulture),
                Tag = [new Coding(CommunicationCodes.SimulatedTagSystem, CommunicationCodes.SimulatedTagCode, CommunicationCodes.SimulatedTagDisplay)],
            },
            Identifier = [new Identifier(CommunicationCodes.IdentifierSystem, id)],
            Status = status,
            Category = [new CodeableConcept(CommunicationCodes.CategorySystem, category)],
            BasedOn = [new ResourceReference($"CommunicationRequest/{message.CommunicationRequestId}")],
            SentElement = new FhirDateTime(occurredAt.ToUniversalTime()),
        };

        if (message.CaseTaskId is { Length: > 0 } caseTaskId)
        {
            communication.About = [new ResourceReference($"Task/{caseTaskId}")];
        }

        if (message.RecipientKind == RecipientKind.Patient)
        {
            communication.Subject = PatientReference(message);
        }

        return communication;
    }

    private static ResourceReference PatientReference(InboxMessage message) =>
        new($"Patient/{message.PatientSyntheticId}", message.PatientDisplayName);

    // A caregiver's copy names no patient, so its report addresses the inbox it was sent to.
    private static ResourceReference RecipientOf(InboxMessage message) =>
        message.RecipientKind == RecipientKind.Patient
            ? PatientReference(message)
            : new ResourceReference
            {
                Identifier = new Identifier(CommunicationCodes.SimulatorInboxSystem, message.PatientSyntheticId),
                Display = message.PatientDisplayName,
            };
}
