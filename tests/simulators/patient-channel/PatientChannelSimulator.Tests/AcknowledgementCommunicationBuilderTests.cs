using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Tests;

/// <summary>Outbound acknowledgement Communication (WS8).</summary>
public class AcknowledgementCommunicationBuilderTests
{
    private static Communication Build() => new AcknowledgementCommunicationBuilder().Build(TestData.Message(), TestData.Now);

    [Fact]
    public void Acknowledgement_has_the_acknowledgement_category()
    {
        var communication = Build();

        Assert.Equal(EventStatus.Completed, communication.Status);
        Assert.Equal("acknowledgement", TestData.CategoryCode(communication));
    }

    [Fact]
    public void Acknowledgement_sender_is_the_patient()
    {
        var communication = Build();

        Assert.Equal($"Patient/{TestData.PatientId}", communication.Sender!.Reference);
        Assert.Equal($"Patient/{TestData.PatientId}", communication.Subject!.Reference);
    }

    [Fact]
    public void Acknowledgement_is_based_on_the_originating_request() => TestData.AssertPointsBack(Build());

    [Fact]
    public void Acknowledgement_carries_the_simulated_tag() => TestData.AssertSimulatedTag(Build());

    [Fact]
    public void Acknowledgement_id_and_version_are_deterministic_for_a_resend()
    {
        var first = Build();
        var second = Build();

        Assert.Equal($"{TestData.RequestId}-acknowledgement", first.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal("1", first.Meta!.VersionId);
        Assert.Equal("1", second.Meta!.VersionId);
    }
}
