using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Translates an inbound FHIR <c>CommunicationRequest</c> into a simulated inbox message.
/// </summary>
/// <remarks>
/// <para>
/// Pure mapping with no side effects. The message body is taken verbatim from the request
/// payload: the simulator must show exactly what it was sent, so a reviewer can verify what
/// reached each channel.
/// </para>
/// <para>
/// CareLoop's simulator provider sends no channel. The inbox is chosen like this: a caregiver's
/// copy (category <c>caregiver</c> in <see cref="RecipientKindSystem"/>) goes to
/// <see cref="InboxChannel.Email"/>; a patient message whose inbox address starts with
/// <see cref="SmsAddressPrefix"/> goes to <see cref="InboxChannel.Sms"/> and one starting with
/// <see cref="PortalAddressPrefix"/> to <see cref="InboxChannel.Portal"/>; anything else goes to
/// <see cref="PatientChannelOptions.DefaultChannel"/>.
/// </para>
/// </remarks>
public sealed class CommunicationRequestMapper
{
    /// <summary>Code system of the recipient kind CareLoop puts in <c>CommunicationRequest.category</c>.</summary>
    public const string RecipientKindSystem = "https://careloop.invalid/fhir/CodeSystem/recipient-kind";

    /// <summary>Recipient-kind code of a message for the patient.</summary>
    public const string PatientKindCode = "patient";

    /// <summary>Recipient-kind code of a caregiver's copy.</summary>
    public const string CaregiverKindCode = "caregiver";

    /// <summary>Prefix every synthetic identifier carries. Anything without it is refused.</summary>
    public const string SyntheticPrefix = "SYN-";

    /// <summary>Inbox-address prefix that places a patient message in the simulated SMS inbox.</summary>
    public const string SmsAddressPrefix = "SYN-SMS-";

    /// <summary>Inbox-address prefix that places a patient message in the simulated portal inbox.</summary>
    public const string PortalAddressPrefix = "SYN-PORTAL-";

    private readonly IOptions<PatientChannelOptions> _options;

    /// <summary>
    /// Creates the mapper.
    /// </summary>
    /// <param name="options">Supplies the inbox used when the inbox address names no channel.</param>
    public CommunicationRequestMapper(IOptions<PatientChannelOptions> options)
    {
        _options = options;
    }

    /// <summary>
    /// Maps a request to a new inbox message in the <see cref="DeliveryState.Pending"/> and
    /// <see cref="ResponseState.None"/> states.
    /// </summary>
    /// <param name="request">The parsed FHIR R4 <c>CommunicationRequest</c>.</param>
    /// <param name="receivedAt">When the simulator received the request.</param>
    /// <returns>The inbox message, marked as simulated. Its identifier is the request's logical id.</returns>
    /// <exception cref="FormatException">
    /// The request has no id, an unknown recipient kind or no text payload; a patient message has
    /// no patient subject; a caregiver's copy has no inbox address; or the identifier of whoever
    /// the message is for is not synthetic (not prefixed <c>SYN-</c>).
    /// </exception>
    public InboxMessage Map(CommunicationRequest request, DateTimeOffset receivedAt)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            throw new FormatException("The CommunicationRequest has no id, so a resend could not be recognised.");
        }

        var kind = RecipientKindOf(request);
        var inboxAddress = request.Recipient
            .Select(r => r.Identifier)
            .FirstOrDefault(i => i is { System: CommunicationCodes.SimulatorInboxSystem } && !string.IsNullOrWhiteSpace(i.Value))?.Value;

        string syntheticId;
        string displayName;
        if (kind == RecipientKind.Caregiver)
        {
            // CareLoop names no patient on a caregiver's copy; the inbox address is the only identity.
            syntheticId = inboxAddress ?? throw new FormatException("The caregiver CommunicationRequest names no simulator inbox as its recipient.");
            displayName = $"Synthetic caregiver {syntheticId}";
        }
        else
        {
            var reference = request.Subject?.Reference;
            if (string.IsNullOrWhiteSpace(reference))
            {
                throw new FormatException("The CommunicationRequest has no patient subject.");
            }

            syntheticId = reference[(reference.LastIndexOf('/') + 1)..];
            displayName = $"Synthetic patient {syntheticId}";
        }

        if (!syntheticId.StartsWith(SyntheticPrefix, StringComparison.Ordinal))
        {
            throw new FormatException($"The identifier '{syntheticId}' is not synthetic (it does not start with {SyntheticPrefix}). The simulator only handles simulated data.");
        }

        var body = request.Payload
            .Select(p => (p.Content as FhirString)?.Value)
            .FirstOrDefault(text => !string.IsNullOrEmpty(text))
            ?? throw new FormatException("The CommunicationRequest has no text payload.");

        return new InboxMessage(
            MessageId: request.Id,
            CommunicationRequestId: request.Id,
            CaseTaskId: CaseTaskIdOf(request),
            PatientSyntheticId: syntheticId,
            PatientDisplayName: displayName,
            Channel: ChannelOf(kind, inboxAddress),
            Language: string.IsNullOrWhiteSpace(request.Language) ? "und" : request.Language,
            Body: body,
            ReceivedAt: receivedAt,
            Delivery: DeliveryState.Pending,
            DeliveryFailureReason: null,
            Response: ResponseState.None,
            RespondedAt: null,
            RecipientKind: kind);
    }

    private static RecipientKind RecipientKindOf(CommunicationRequest request)
    {
        var code = request.Category
            .SelectMany(c => c.Coding)
            .FirstOrDefault(c => c.System == RecipientKindSystem)?.Code;

        return code switch
        {
            null or PatientKindCode => RecipientKind.Patient,
            CaregiverKindCode => RecipientKind.Caregiver,
            _ => throw new FormatException($"The recipient kind '{code}' is not one the patient channel handles."),
        };
    }

    private static string? CaseTaskIdOf(CommunicationRequest request)
    {
        const string prefix = "Task/";
        var reference = request.About
            .Select(r => r.Reference)
            .FirstOrDefault(r => r is not null && r.StartsWith(prefix, StringComparison.Ordinal) && r.Length > prefix.Length);
        return reference?[prefix.Length..];
    }

    private InboxChannel ChannelOf(RecipientKind kind, string? inboxAddress)
    {
        if (kind == RecipientKind.Caregiver)
        {
            return InboxChannel.Email;
        }

        if (inboxAddress is not null && inboxAddress.StartsWith(SmsAddressPrefix, StringComparison.Ordinal))
        {
            return InboxChannel.Sms;
        }

        if (inboxAddress is not null && inboxAddress.StartsWith(PortalAddressPrefix, StringComparison.Ordinal))
        {
            return InboxChannel.Portal;
        }

        return _options.Value.DefaultChannel;
    }
}
