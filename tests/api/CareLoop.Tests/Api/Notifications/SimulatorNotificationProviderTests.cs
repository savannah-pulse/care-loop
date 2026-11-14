using System.Net;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Domain.Recipients;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Options;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;
using ContactPoint = CareLoop.Domain.Recipients.ContactPoint;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// The simulator provider delivers patient messages to the patient-channel simulator and staff messages to the HIS simulator.
/// </summary>
public sealed class SimulatorNotificationProviderTests
{
    private static readonly Guid CaseId = Guid.Parse("0f3c2c55-0000-4000-8000-000000000001");

    private readonly RecordingHandler _http = new();

    [Fact]
    public async System.Threading.Tasks.Task Patient_message_goes_to_the_patient_channel_simulator()
    {
        var result = await Provider().SendAsync(Message(RecipientKind.Patient), CancellationToken.None);

        Assert.True(result.Accepted);
        var request = Assert.Single(_http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://patient-channel.invalid/fhir/CommunicationRequest", request.Uri.ToString());
        Assert.Equal(FhirEndpoints.FhirJson, request.ContentType);
    }

    [Fact]
    public async System.Threading.Tasks.Task Staff_message_goes_to_the_his_simulator_staff_inbox()
    {
        var message = Message(RecipientKind.Staff, address: "SYN-QUEUE-FAC-A", body: "A case needs your action. Open it in the HIS: Task/x");

        var result = await Provider().SendAsync(message, CancellationToken.None);

        Assert.True(result.Accepted);
        var request = Assert.Single(_http.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal($"http://his.invalid/fhir/Task/staff-notice-{message.AttemptId:N}", request.Uri.ToString());
        var task = Assert.IsType<FhirTask>(FhirJsonCodec.Deserialize(request.Body));
        Assert.Equal(SimulatorNotificationProvider.StaffNoticeTaskCode, StatusMap.CodeIn(task.Code, StatusMap.TaskCodeSystem));
        Assert.Equal($"Task/{CaseId:D}", task.Focus!.Reference);
        Assert.Equal("SYN-QUEUE-FAC-A", task.Owner!.Identifier!.Value);
        Assert.Contains("A case needs your action", task.Description);
        Assert.Equal($"Task/staff-notice-{message.AttemptId:N}", result.ProviderMessageId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Patient_message_is_sent_as_a_CommunicationRequest_with_the_attempt_id()
    {
        var message = Message(RecipientKind.Patient);

        await Provider().SendAsync(message, CancellationToken.None);

        var request = Assert.IsType<CommunicationRequest>(FhirJsonCodec.Deserialize(Assert.Single(_http.Requests).Body));
        Assert.Equal(message.AttemptId.ToString("D"), request.Id);
        Assert.Equal(message.AttemptId.ToString("D"), Assert.Single(request.Identifier, i => i.System == SimulatorNotificationProvider.AttemptIdentifierSystem).Value);
        Assert.Equal("patient", StatusMap.CodeIn(Assert.Single(request.Category), SimulatorNotificationProvider.RecipientKindSystem));
        Assert.Equal("Patient/SYN-PT-0001", request.Subject!.Reference);
        Assert.Equal("SYN-INBOX-0001", Assert.Single(request.Recipient).Identifier!.Value);
        Assert.Equal($"Task/{CaseId:D}", Assert.Single(request.About).Reference);
        Assert.Equal(message.Body, Assert.IsType<FhirString>(Assert.Single(request.Payload).Content).Value);
        Assert.Equal("en", request.Language);
    }

    [Fact]
    public void Caregiver_message_id_is_marked_as_a_caregiver_message()
    {
        var caregiver = Message(RecipientKind.Caregiver);

        var request = SimulatorNotificationProvider.CommunicationRequestFor(caregiver);

        Assert.Equal(SimulatorNotificationProvider.CaregiverIdPrefix + caregiver.AttemptId.ToString("D"), request.Id);
        Assert.Equal(caregiver.AttemptId.ToString("D"), Assert.Single(request.Identifier).Value);
        Assert.Equal("caregiver", StatusMap.CodeIn(Assert.Single(request.Category), SimulatorNotificationProvider.RecipientKindSystem));
        Assert.Null(request.Subject);
    }

    [Fact]
    public void Cannot_reach_a_recipient_without_a_simulator_contact_point()
    {
        var provider = Provider();
        var smsOnly = Message(RecipientKind.Patient).Recipient with
        {
            ContactPoints = [new ContactPoint { Channel = NotificationChannel.Sms, Address = "SYN-SMS-0001" }],
        };

        Assert.False(provider.CanReach(smsOnly));
        Assert.False(provider.CanReach(smsOnly with { ContactPoints = [] }));
        Assert.True(provider.CanReach(Message(RecipientKind.Patient).Recipient));
    }

    [Fact]
    public async System.Threading.Tasks.Task Refused_or_unconfigured_send_is_reported_not_thrown()
    {
        var refusing = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var refused = await Provider(refusing).SendAsync(Message(RecipientKind.Patient), CancellationToken.None);
        var unconfigured = await new SimulatorNotificationProvider(new HttpClient(_http), Options.Create(new SimulatorNotificationOptions()))
            .SendAsync(Message(RecipientKind.Staff), CancellationToken.None);

        Assert.False(refused.Accepted);
        Assert.Contains("503", refused.FailureReason);
        Assert.False(unconfigured.Accepted);
        Assert.Contains("StaffInboxBaseUrl", unconfigured.FailureReason);
        Assert.Empty(_http.Requests);
    }

    private SimulatorNotificationProvider Provider(RecordingHandler? handler = null) =>
        new(new HttpClient(handler ?? _http), Options.Create(new SimulatorNotificationOptions
        {
            PatientChannelBaseUrl = "http://patient-channel.invalid/",
            StaffInboxBaseUrl = "http://his.invalid",
        }));

    private static NotificationMessage Message(RecipientKind kind, string address = "SYN-INBOX-0001", string body = "[PLACEHOLDER] Reference: AB12CD34")
    {
        var contactPoint = new ContactPoint { Channel = NotificationChannel.Simulator, Address = address };
        return new NotificationMessage
        {
            AttemptId = Guid.NewGuid(),
            CaseId = CaseId,
            Recipient = new Recipient { Reference = kind == RecipientKind.Patient ? "SYN-PT-0001" : address, Kind = kind, Language = "en", ContactPoints = [contactPoint] },
            ContactPoint = contactPoint,
            TemplateKey = "patient-follow-up",
            Body = body,
            IsSmsSafe = false,
        };
    }
}
