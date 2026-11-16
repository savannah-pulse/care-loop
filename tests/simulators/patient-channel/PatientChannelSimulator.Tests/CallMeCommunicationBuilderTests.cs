using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Tests;

/// <summary>Outbound call-me Communication (WS8).</summary>
public class CallMeCommunicationBuilderTests
{
    private static Communication Build() => new CallMeCommunicationBuilder().Build(TestData.Message(), TestData.Now);

    [Fact]
    public void Call_me_has_the_call_me_category_distinct_from_acknowledgement()
    {
        var communication = Build();

        Assert.Equal(EventStatus.Completed, communication.Status);
        Assert.Equal("call-me", TestData.CategoryCode(communication));
        Assert.NotEqual(CommunicationCodes.Acknowledgement, TestData.CategoryCode(communication));
    }

    [Fact]
    public void Call_me_sender_is_the_patient() =>
        Assert.Equal($"Patient/{TestData.PatientId}", Build().Sender!.Reference);

    [Fact]
    public void Call_me_carries_the_simulated_tag() => TestData.AssertSimulatedTag(Build());

    [Fact]
    public void Call_me_points_back_to_the_request_and_the_case() => TestData.AssertPointsBack(Build());

    [Fact]
    public void A_further_call_me_request_gets_the_next_version_and_a_resend_keeps_it()
    {
        var builder = new CallMeCommunicationBuilder();
        var first = builder.Build(TestData.Message(), TestData.Now);
        var resend = builder.Build(TestData.Message(), TestData.Now);
        var further = builder.Build(TestData.Message() with { CallRequestCount = 1 }, TestData.Now);

        Assert.Equal("1", first.Meta!.VersionId);
        Assert.Equal("1", resend.Meta!.VersionId);
        Assert.Equal("2", further.Meta!.VersionId);
        Assert.Equal(first.Id, further.Id);
    }
}
