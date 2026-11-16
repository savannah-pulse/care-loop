using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Tests;

/// <summary>Outbound failed-delivery Communication (WS8).</summary>
public class DeliveryFailedCommunicationBuilderTests
{
    private const string Reason = "SIMULATED: handset unreachable";

    private static Communication Build() => new DeliveryFailedCommunicationBuilder().Build(TestData.Message(), TestData.Now, Reason);

    [Fact]
    public void Failed_communication_has_status_not_done_and_delivery_category()
    {
        var communication = Build();

        Assert.Equal(EventStatus.NotDone, communication.Status);
        Assert.Equal("delivery-status", TestData.CategoryCode(communication));
    }

    [Fact]
    public void Failed_communication_carries_the_reason_in_statusReason()
    {
        Assert.Equal(Reason, Build().StatusReason!.Text);
        Assert.Throws<ArgumentException>(() => new DeliveryFailedCommunicationBuilder().Build(TestData.Message(), TestData.Now, " "));
    }

    [Fact]
    public void Failed_communication_is_based_on_the_originating_request() => TestData.AssertPointsBack(Build());

    [Fact]
    public void Failed_communication_carries_the_simulated_tag() => TestData.AssertSimulatedTag(Build());

    [Fact]
    public void Failed_and_delivered_reports_share_one_identity_because_a_message_has_one_outcome() =>
        Assert.Equal(new DeliveredCommunicationBuilder().Build(TestData.Message(), TestData.Now).Id, Build().Id);
}
