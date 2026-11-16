using System.Net;
using System.Text.Json;
using CareLoop.Api.Features.Notifications;
using CareLoop.Api.Infrastructure.Notifications;
using CareLoop.Domain.Recipients;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;

namespace CareLoop.Tests.Api.Notifications;

/// <summary>
/// The text-message and email providers post plain JSON to the SMS and email simulator: no FHIR travels on the notification path.
/// </summary>
public sealed class SimulatorNotificationProviderTests
{
    private static readonly Guid CaseId = Guid.Parse("0f3c2c55-0000-4000-8000-000000000001");

    private readonly RecordingHandler _http = new();

    [Fact]
    public async Task Text_message_is_posted_to_the_simulator_as_json()
    {
        var message = Message(RecipientKind.Patient, NotificationChannel.Sms);

        var result = await Sms().SendAsync(message, CancellationToken.None);

        Assert.True(result.Accepted);
        Assert.Equal($"sms/{message.AttemptId:D}", result.ProviderMessageId);
        var request = Assert.Single(_http.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://simulator.invalid/messages", request.Uri.ToString());
        Assert.Equal("application/json", request.ContentType);
        var sent = JsonDocument.Parse(request.Body).RootElement;
        Assert.Equal(message.AttemptId.ToString("D"), sent.GetProperty("messageId").GetString());
        Assert.Equal("sms", sent.GetProperty("channel").GetString());
        Assert.Equal("SYN-TEL-0001", sent.GetProperty("to").GetString());
        Assert.Equal(message.Body, sent.GetProperty("body").GetString());
        Assert.Equal("en", sent.GetProperty("language").GetString());
        Assert.Equal(CaseId.ToString("D"), sent.GetProperty("metadata").GetProperty("caseId").GetString());
        Assert.Equal("patient", sent.GetProperty("metadata").GetProperty("audience").GetString());
    }

    [Fact]
    public async Task Message_carries_no_fhir_and_no_patient_identity()
    {
        await Sms().SendAsync(Message(RecipientKind.Patient, NotificationChannel.Sms), CancellationToken.None);

        var body = Assert.Single(_http.Requests).Body;
        Assert.DoesNotContain("resourceType", body, StringComparison.Ordinal);
        Assert.DoesNotContain("SYN-PT-0001", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_email_carries_a_subject()
    {
        var withSubject = Message(RecipientKind.Patient, NotificationChannel.Email) with { Subject = "A follow-up step is waiting for you" };

        Assert.Equal("A follow-up step is waiting for you", Email().SimulatorMessageFor(withSubject).Subject);
        Assert.Null(Sms().SimulatorMessageFor(withSubject with { ContactPoint = Contact(NotificationChannel.Sms) }).Subject);
    }

    [Theory]
    [InlineData(RecipientKind.Patient, "patient")]
    [InlineData(RecipientKind.Caregiver, "caregiver")]
    [InlineData(RecipientKind.Staff, "staff")]
    public void Metadata_says_who_the_message_was_for(RecipientKind kind, string audience)
    {
        Assert.Equal(audience, Email().SimulatorMessageFor(Message(kind, NotificationChannel.Email)).Metadata.Audience);
    }

    [Fact]
    public void Text_messages_get_the_sms_template_and_email_the_standard_one()
    {
        Assert.False(Sms().AllowsSensitiveContent);
        Assert.True(Email().AllowsSensitiveContent);
        Assert.Equal(NotificationChannel.Sms, Sms().Channel);
        Assert.Equal(NotificationChannel.Email, Email().Channel);
    }

    [Fact]
    public void Cannot_reach_a_recipient_without_a_contact_point_on_its_channel()
    {
        var emailOnly = Message(RecipientKind.Patient, NotificationChannel.Email).Recipient;

        Assert.False(Sms().CanReach(emailOnly));
        Assert.False(Sms().CanReach(emailOnly with { ContactPoints = [] }));
        Assert.False(Email().CanReach(emailOnly with { ContactPoints = [new ContactPoint { Channel = NotificationChannel.Email, Address = " " }] }));
        Assert.True(Email().CanReach(emailOnly));
    }

    [Fact]
    public async Task Refused_or_unconfigured_send_is_reported_not_thrown()
    {
        var refusing = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var failing = new RecordingHandler(_ => throw new HttpRequestException("connection refused"));
        var message = Message(RecipientKind.Patient, NotificationChannel.Sms);

        var refused = await Sms(refusing).SendAsync(message, CancellationToken.None);
        var unreachable = await Sms(failing).SendAsync(message, CancellationToken.None);
        var unconfigured = await new SmsNotificationProvider(new HttpClient(_http), NotificationTestSupport.Simulator())
            .SendAsync(message, CancellationToken.None);

        Assert.False(refused.Accepted);
        Assert.Contains("503", refused.FailureReason);
        Assert.False(unreachable.Accepted);
        Assert.Contains("could not be reached", unreachable.FailureReason);
        Assert.False(unconfigured.Accepted);
        Assert.Contains("Notifications:Sms:BaseUrl", unconfigured.FailureReason);
        Assert.Empty(_http.Requests);
    }

    private SmsNotificationProvider Sms(RecordingHandler? handler = null) =>
        new(new HttpClient(handler ?? _http), NotificationTestSupport.Simulator("http://simulator.invalid/"));

    private EmailNotificationProvider Email() =>
        new(new HttpClient(_http), NotificationTestSupport.Simulator("http://simulator.invalid"));

    private static ContactPoint Contact(NotificationChannel channel) =>
        new() { Channel = channel, Address = channel == NotificationChannel.Sms ? "SYN-TEL-0001" : "SYN-EMAIL-0001" };

    private static NotificationMessage Message(RecipientKind kind, NotificationChannel channel)
    {
        var contactPoint = Contact(channel);
        return new NotificationMessage
        {
            AttemptId = Guid.NewGuid(),
            CaseId = CaseId,
            Recipient = new Recipient { Reference = kind == RecipientKind.Patient ? "SYN-PT-0001" : "SYN-OTHER-0001", Kind = kind, Language = "en", ContactPoints = [contactPoint] },
            ContactPoint = contactPoint,
            TemplateKey = "patient-follow-up",
            Body = "[PLACEHOLDER] Reference: AB12CD34",
            IsSmsSafe = channel == NotificationChannel.Sms,
        };
    }
}
