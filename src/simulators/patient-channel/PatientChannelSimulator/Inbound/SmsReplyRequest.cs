namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Body of <c>POST /api/inbox/{messageId}/reply</c>: the text a patient sends back to an SMS (C7).
/// </summary>
/// <param name="Text">What the patient typed. <c>1</c> acknowledges and <c>2</c> asks for a call; anything else is refused.</param>
public sealed record SmsReplyRequest(string? Text);
