using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Tests;

/// <summary>Outbound confidence-rating Communication (WS8, design gap C10).</summary>
public class ConfidenceRatingCommunicationBuilderTests
{
    private static Communication Build(int rating = 4) =>
        new ConfidenceRatingCommunicationBuilder().Build(TestData.Message(), TestData.Now, rating);

    [Fact]
    public void Rating_has_its_own_category_and_is_not_an_acknowledgement()
    {
        var communication = Build();

        Assert.Equal(EventStatus.Completed, communication.Status);
        Assert.Equal("confidence-rating", TestData.CategoryCode(communication));
        Assert.DoesNotContain(communication.Category.SelectMany(c => c.Coding), c => c.Code == CommunicationCodes.Acknowledgement);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public void Rating_travels_as_an_integer_in_the_first_payload_string(int rating)
    {
        var payload = Assert.Single(Build(rating).Payload);

        Assert.Equal(rating.ToString(System.Globalization.CultureInfo.InvariantCulture), Assert.IsType<FhirString>(payload.Content).Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Rating_outside_one_to_five_is_refused(int rating) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(rating));

    [Fact]
    public void Rating_sender_is_the_patient() =>
        Assert.Equal($"Patient/{TestData.PatientId}", Build().Sender!.Reference);

    [Fact]
    public void Rating_carries_the_simulated_tag() => TestData.AssertSimulatedTag(Build());

    [Fact]
    public void Rating_points_back_to_the_request_and_the_case() => TestData.AssertPointsBack(Build());

    [Fact]
    public void A_further_rating_gets_the_next_version()
    {
        var builder = new ConfidenceRatingCommunicationBuilder();
        var first = builder.Build(TestData.Message(), TestData.Now, 2);
        var further = builder.Build(TestData.Message() with { ConfidenceRatingCount = 1 }, TestData.Now, 5);

        Assert.Equal("1", first.Meta!.VersionId);
        Assert.Equal("2", further.Meta!.VersionId);
        Assert.Equal(first.Id, further.Id);
    }

    [Fact]
    public void Longest_possible_id_is_a_valid_fhir_id()
    {
        var message = TestData.Message(requestId: "caregiver-" + TestData.RequestId);

        var id = new ConfidenceRatingCommunicationBuilder().Build(message, TestData.Now, 3).Id;

        Assert.Matches("^[A-Za-z0-9\\-\\.]{1,64}$", id);
    }
}
