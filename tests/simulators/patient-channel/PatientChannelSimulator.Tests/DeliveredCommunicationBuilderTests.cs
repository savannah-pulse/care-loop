using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>Outbound delivered Communication (WS8).</summary>
public class DeliveredCommunicationBuilderTests
{
    private static Communication Build() => new DeliveredCommunicationBuilder().Build(TestData.Message(), TestData.Now);

    [Fact]
    public void Delivered_communication_has_status_completed_and_delivery_category()
    {
        var communication = Build();

        Assert.Equal(EventStatus.Completed, communication.Status);
        Assert.Equal("delivery-status", TestData.CategoryCode(communication));
        Assert.Equal($"Patient/{TestData.PatientId}", Assert.Single(communication.Recipient).Reference);
        Assert.Null(communication.Sender!.Reference);
        Assert.Equal(CommunicationCodes.ChannelDeviceIdentifier, communication.Sender!.Identifier!.Value);
    }

    [Fact]
    public void Delivered_communication_is_based_on_the_originating_request() => TestData.AssertPointsBack(Build());

    [Fact]
    public void Delivered_communication_never_carries_a_patient_response_category()
    {
        var codes = Build().Category.SelectMany(c => c.Coding).Select(c => c.Code).ToList();

        Assert.Equal([CommunicationCodes.DeliveryStatus], codes);
    }

    [Fact]
    public void Delivered_communication_carries_the_simulated_tag() => TestData.AssertSimulatedTag(Build());

    [Fact]
    public void Delivered_communication_id_and_version_are_deterministic_for_a_resend()
    {
        var first = Build();
        var second = new DeliveredCommunicationBuilder().Build(TestData.Message(), TestData.Now.AddMinutes(5));

        Assert.Equal($"{TestData.RequestId}-delivery", first.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("1", first.Meta!.VersionId);
        Assert.Equal("1", second.Meta!.VersionId);
    }

    [Fact]
    public void Delivered_report_for_a_caregiver_copy_names_the_inbox_and_no_patient()
    {
        var message = TestData.Message(requestId: "caregiver-" + TestData.RequestId, patientId: "SYN-CG-INBOX-0001", recipientKind: RecipientKind.Caregiver);

        var communication = new DeliveredCommunicationBuilder().Build(message, TestData.Now);

        Assert.Null(communication.Subject);
        var recipient = Assert.Single(communication.Recipient);
        Assert.Null(recipient.Reference);
        Assert.Equal("SYN-CG-INBOX-0001", recipient.Identifier!.Value);
        Assert.Equal($"CommunicationRequest/caregiver-{TestData.RequestId}", Assert.Single(communication.BasedOn).Reference);
        Assert.True(communication.Id!.Length <= 64);
    }
}
