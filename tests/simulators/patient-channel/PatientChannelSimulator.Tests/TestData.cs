using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>Synthetic values shared by the tests.</summary>
internal static class TestData
{
    public const string MessageId = "0d6f3c1e-5b0a-4f6e-9c56-2f1f3d0a7b11";
    public const string CaseId = "7a1c9e42-3f7b-4c1d-8a55-6e0b1d2c3f44";
    public const string PatientId = "SYN-PT-0001";
    public const string Body = "SIMULATED: a follow-up step is waiting. Reply 1 to confirm you got this message. Reply 2 for a call.";

    public static readonly DateTimeOffset Now = new(2026, 3, 2, 9, 30, 0, TimeSpan.Zero);

    public static InboxMessage Message(
        string messageId = MessageId,
        string to = PatientId,
        DateTimeOffset? receivedAt = null,
        InboxChannel channel = InboxChannel.Sms,
        RecipientKind recipientKind = RecipientKind.Patient,
        DeliveryState delivery = DeliveryState.Delivered,
        string? caseId = CaseId) =>
        new(
            MessageId: messageId,
            CaseId: caseId,
            To: to,
            DisplayName: $"Synthetic patient {to}",
            Channel: channel,
            Subject: null,
            Language: "en",
            Body: Body,
            ReceivedAt: receivedAt ?? Now,
            Delivery: delivery,
            DeliveryFailureReason: null,
            RecipientKind: recipientKind,
            Replies: []);
}
