namespace PatientChannelSimulator.State;

/// <summary>
/// One entry in the simulator's message log: a single request and response exchanged with CareLoop.
/// </summary>
/// <remarks>
/// The log exists so a demo audience can see exactly what an SMS or email service would be sent
/// and what it would send back. Bodies are synthetic JSON; <paramref name="Simulated"/> is always
/// <see langword="true"/>.
/// </remarks>
/// <param name="EntryId">Simulator-local identifier of the log entry.</param>
/// <param name="At">When the exchange happened, taken from the simulator clock.</param>
/// <param name="Direction">Whether CareLoop called the simulator or the reverse.</param>
/// <param name="Method">HTTP method of the exchange.</param>
/// <param name="Url">Request URL or path of the exchange.</param>
/// <param name="Kind">What was exchanged: <c>message</c>, <c>delivery</c> or <c>reply</c>.</param>
/// <param name="MessageId">The message the exchange is about, when known.</param>
/// <param name="StatusCode">HTTP status code of the response; <see langword="null"/> when the call failed before a response arrived.</param>
/// <param name="Body">The JSON that was sent; <see langword="null"/> when a refused message was not kept.</param>
/// <param name="Simulated">Visible simulated-data marker; always <see langword="true"/>.</param>
public sealed record MessageLogEntry(
    string EntryId,
    DateTimeOffset At,
    MessageDirection Direction,
    string Method,
    string Url,
    string Kind,
    string? MessageId,
    int? StatusCode,
    string? Body,
    bool Simulated = true)
{
    /// <summary>Kind of a message CareLoop sent.</summary>
    public const string MessageKind = "message";

    /// <summary>Kind of a delivery report sent to CareLoop.</summary>
    public const string DeliveryKind = "delivery";

    /// <summary>Kind of a reply sent to CareLoop.</summary>
    public const string ReplyKind = "reply";
}

/// <summary>
/// Direction of a logged exchange, seen from the simulator.
/// </summary>
public enum MessageDirection
{
    /// <summary>CareLoop called the simulator: a text message or an email to deliver.</summary>
    Inbound,

    /// <summary>The simulator called CareLoop: a delivery report or a reply.</summary>
    Outbound,
}
