using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Tests;

/// <summary>Outbound teach-back Communication (WS8).</summary>
public class TeachBackCommunicationBuilderTests
{
    private static Communication Build() => new TeachBackCommunicationBuilder().Build(TestData.Message(), TestData.Now);

    [Fact]
    public void Teach_back_has_the_teach_back_category_distinct_from_acknowledgement()
    {
        var communication = Build();

        Assert.Equal(EventStatus.Completed, communication.Status);
        Assert.Equal("teach-back", TestData.CategoryCode(communication));
        Assert.NotEqual(CommunicationCodes.Acknowledgement, TestData.CategoryCode(communication));
        Assert.NotEqual(new AcknowledgementCommunicationBuilder().Build(TestData.Message(), TestData.Now).Id, communication.Id);
    }

    [Fact]
    public void Teach_back_sender_is_the_patient() =>
        Assert.Equal($"Patient/{TestData.PatientId}", Build().Sender!.Reference);

    [Fact]
    public void Teach_back_carries_the_simulated_tag() => TestData.AssertSimulatedTag(Build());

    [Fact]
    public void Teach_back_points_back_to_the_request_and_the_case() => TestData.AssertPointsBack(Build());
}
