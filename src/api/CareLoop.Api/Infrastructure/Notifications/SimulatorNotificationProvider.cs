using System.Text;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Notifications;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Notification provider that delivers to the simulators instead of a real channel.
/// </summary>
/// <remarks>
/// <para>
/// When this provider is enabled, notifications go to the simulators, chosen by <see cref="Recipient.Kind"/>.
/// The wire format is private to this provider and the two simulators:
/// </para>
/// <list type="bullet">
/// <item><description>
/// A patient or caregiver message is a FHIR <c>CommunicationRequest</c> posted to
/// <c>{PatientChannelBaseUrl}/fhir/CommunicationRequest</c>. Its <see cref="AttemptIdentifierSystem"/> identifier
/// is the notification attempt identifier, and so is its logical id, prefixed with <see cref="CaregiverIdPrefix"/>
/// for a caregiver message so that what comes back about it is never taken for the patient's; its category in
/// <see cref="RecipientKindSystem"/> is <c>patient</c> or <c>caregiver</c> (a caregiver message belongs in the
/// simulator's Email inbox); <c>subject</c> is the patient; <c>recipient</c> names the simulator inbox in
/// <see cref="SimulatorInboxSystem"/>; <c>about</c> is the case <c>Task</c>; <c>payload</c> is the rendered text;
/// <c>language</c> is the recipient's language. A replay carries the same id, so the simulator can drop it.
/// The simulator answers with <c>Communication</c>s that point back through <c>basedOn</c> and <c>about</c>
/// (see <see cref="SimulatorInboundNotificationHandler"/>).
/// </description></item>
/// <item><description>
/// A staff message is a FHIR <c>Task</c> with code <see cref="StaffNoticeTaskCode"/>, sent with
/// <c>PUT {StaffInboxBaseUrl}/fhir/Task/staff-notice-{attempt}</c> so a replay overwrites rather than duplicates.
/// <c>owner</c> names the staff inbox in <see cref="SimulatorInboxSystem"/>, <c>focus</c> is the case <c>Task</c>
/// and <c>description</c> is the rendered staff text, which carries no patient detail.
/// </description></item>
/// </list>
/// <para>
/// What the simulator sends back (delivery reports and the patient's replies) arrives through
/// this provider's inbound half, <see cref="SimulatorInboundNotificationHandler"/>.
/// </para>
/// </remarks>
public sealed class SimulatorNotificationProvider : INotificationProvider
{
    private const string Base = "https://careloop.invalid/fhir";

    /// <summary>Identifier system of the notification attempt identifier.</summary>
    public const string AttemptIdentifierSystem = Base + "/sid/notification-attempt";

    /// <summary>Identifier system of a simulator inbox address (the contact point address).</summary>
    public const string SimulatorInboxSystem = Base + "/sid/simulator-inbox";

    /// <summary>Code system of the recipient kind carried as the <c>CommunicationRequest</c> category.</summary>
    public const string RecipientKindSystem = Base + "/CodeSystem/recipient-kind";

    /// <summary>
    /// Prefix of the logical id of a caregiver message's <c>CommunicationRequest</c>.
    /// </summary>
    /// <remarks>
    /// The case's notification track follows the patient only. A <c>Communication</c> based on a request with this
    /// prefix (a caregiver's delivery report or reply) is therefore ignored by
    /// <see cref="SimulatorInboundNotificationHandler"/> rather than recorded as the patient's.
    /// </remarks>
    public const string CaregiverIdPrefix = "caregiver-";

    /// <summary>Task code, in <see cref="StatusMap.TaskCodeSystem"/>, of a direct staff notice.</summary>
    public const string StaffNoticeTaskCode = "staff-notice";

    private readonly HttpClient _http;
    private readonly IOptions<SimulatorNotificationOptions> _endpoints;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="http">The HTTP client supplied by the HTTP client factory.</param>
    /// <param name="endpoints">Where the simulators can be reached.</param>
    public SimulatorNotificationProvider(HttpClient http, IOptions<SimulatorNotificationOptions> endpoints)
    {
        _http = http;
        _endpoints = endpoints;
    }

    /// <inheritdoc/>
    public NotificationChannel Channel => NotificationChannel.Simulator;

    /// <inheritdoc/>
    public bool AllowsSensitiveContent => true;

    /// <inheritdoc/>
    public bool CanReach(Recipient recipient)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        return recipient.ContactPoints.Any(c => c.Channel == NotificationChannel.Simulator && !string.IsNullOrWhiteSpace(c.Address));
    }

    /// <inheritdoc/>
    public async Task<SendResult> SendAsync(NotificationMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);
        var staff = message.Recipient.Kind == RecipientKind.Staff;
        var baseUrl = staff ? _endpoints.Value.StaffInboxBaseUrl : _endpoints.Value.PatientChannelBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var setting = staff ? nameof(SimulatorNotificationOptions.StaffInboxBaseUrl) : nameof(SimulatorNotificationOptions.PatientChannelBaseUrl);
            return Refused($"{SimulatorNotificationOptions.SectionName}:{setting} is not configured.");
        }

        Resource resource = staff ? StaffNotice(message) : CommunicationRequestFor(message);
        var root = baseUrl.TrimEnd('/');
        using var request = staff
            ? new HttpRequestMessage(HttpMethod.Put, $"{root}/fhir/Task/{Uri.EscapeDataString(resource.Id!)}")
            : new HttpRequestMessage(HttpMethod.Post, $"{root}/fhir/CommunicationRequest");
        request.Content = new StringContent(FhirJsonCodec.Serialize(resource), Encoding.UTF8, FhirEndpoints.FhirJson);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            return response.IsSuccessStatusCode
                ? new SendResult { Accepted = true, ProviderMessageId = $"{resource.TypeName}/{resource.Id}" }
                : Refused($"The simulator answered {(int)response.StatusCode} {response.ReasonPhrase}.");
        }
        catch (HttpRequestException ex)
        {
            return Refused($"The simulator could not be reached: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Refused("The simulator did not answer in time.");
        }
    }

    /// <summary>
    /// Builds the <c>CommunicationRequest</c> for a patient or caregiver message.
    /// </summary>
    /// <param name="message">The rendered message.</param>
    /// <returns>The resource to post to the patient-channel simulator.</returns>
    public static CommunicationRequest CommunicationRequestFor(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var kind = message.Recipient.Kind == RecipientKind.Caregiver ? "caregiver" : "patient";
        return new CommunicationRequest
        {
            Id = message.Recipient.Kind == RecipientKind.Caregiver
                ? CaregiverIdPrefix + message.AttemptId.ToString("D")
                : message.AttemptId.ToString("D"),
            Identifier = [new Identifier(AttemptIdentifierSystem, message.AttemptId.ToString("D"))],
            Status = RequestStatus.Active,
            Category = [new CodeableConcept(RecipientKindSystem, kind)],
            Subject = message.Recipient.Kind == RecipientKind.Patient
                ? new ResourceReference(Typed("Patient", message.Recipient.Reference))
                : null,
            Recipient = [new ResourceReference { Identifier = new Identifier(SimulatorInboxSystem, message.ContactPoint.Address) }],
            About = [new ResourceReference($"Task/{message.CaseId:D}")],
            Payload = [new CommunicationRequest.PayloadComponent { Content = new FhirString(message.Body) }],
            Language = message.Recipient.Language,
        };
    }

    /// <summary>
    /// Builds the staff-notice <c>Task</c> for a staff message.
    /// </summary>
    /// <param name="message">The rendered message; it carries no patient detail.</param>
    /// <returns>The resource to put to the HIS simulator.</returns>
    public static FhirTask StaffNotice(NotificationMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new FhirTask
        {
            Id = $"staff-notice-{message.AttemptId:N}",
            Identifier = [new Identifier(AttemptIdentifierSystem, message.AttemptId.ToString("D"))],
            Status = FhirTask.TaskStatus.Requested,
            Intent = FhirTask.TaskIntent.Order,
            Code = StatusMap.TaskCode(StaffNoticeTaskCode),
            Focus = new ResourceReference($"Task/{message.CaseId:D}"),
            Owner = new ResourceReference { Identifier = new Identifier(SimulatorInboxSystem, message.ContactPoint.Address) },
            Description = message.Subject is null ? message.Body : $"{message.Subject}\n\n{message.Body}",
        };
    }

    private static string Typed(string type, string reference) =>
        reference.Contains('/', StringComparison.Ordinal) ? reference : $"{type}/{reference}";

    private static SendResult Refused(string reason) => new() { Accepted = false, FailureReason = reason };
}
