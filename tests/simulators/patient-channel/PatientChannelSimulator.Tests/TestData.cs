using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>Synthetic values shared by the tests.</summary>
internal static class TestData
{
    public const string RequestId = "0d6f3c1e-5b0a-4f6e-9c56-2f1f3d0a7b11";
    public const string CaseId = "7a1c9e42-3f7b-4c1d-8a55-6e0b1d2c3f44";
    public const string PatientId = "SYN-PT-0001";

    public static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 30, 0, TimeSpan.Zero);

    public static InboxMessage Message(
        string requestId = RequestId,
        string patientId = PatientId,
        DateTimeOffset? receivedAt = null,
        InboxChannel channel = InboxChannel.App,
        RecipientKind recipientKind = RecipientKind.Patient,
        DeliveryState delivery = DeliveryState.Delivered) =>
        new(
            MessageId: requestId,
            CommunicationRequestId: requestId,
            CaseTaskId: CaseId,
            PatientSyntheticId: patientId,
            PatientDisplayName: $"Synthetic patient {patientId}",
            Channel: channel,
            Language: "en",
            Body: "SIMULATED: please contact the clinic about your follow-up.",
            ReceivedAt: receivedAt ?? Now,
            Delivery: delivery,
            DeliveryFailureReason: null,
            Response: ResponseState.None,
            RespondedAt: null,
            RecipientKind: recipientKind);

    public static string? CategoryCode(Communication communication) =>
        communication.Category
            .SelectMany(c => c.Coding)
            .SingleOrDefault(c => c.System == CommunicationCodes.CategorySystem)?.Code;

    public static void AssertSimulatedTag(Communication communication)
    {
        var tag = Assert.Single(communication.Meta!.Tag);
        Assert.Equal("https://careloop.invalid/fhir/CodeSystem/data-origin", tag.System);
        Assert.Equal("simulated", tag.Code);
        Assert.Empty(communication.Meta!.Security);
    }

    public static void AssertPointsBack(Communication communication)
    {
        Assert.Equal($"CommunicationRequest/{RequestId}", Assert.Single(communication.BasedOn).Reference);
        Assert.Equal($"Task/{CaseId}", Assert.Single(communication.About).Reference);
        Assert.Equal(Now, communication.SentElement!.ToDateTimeOffset(TimeSpan.Zero));
    }
}
