namespace CareLoop.Domain.Events;

/// <summary>
/// Where an event came from, when it did not come from the hospital system the case belongs to.
/// </summary>
/// <remarks>
/// A case is found by its source system and result identifier, so every event about it carries the case's source
/// system, whoever sent it. The origin says who actually did: CareLoop itself, or a text message or email channel.
/// An event with no origin came from the source system, normally a hospital system over FHIR. Keeping this apart
/// lets the audit trail and the timeline say plainly where each entry came from, with nothing guessed.
/// </remarks>
public static class EventOrigins
{
    /// <summary>CareLoop itself: a person working in CareLoop, a deadline check, or CareLoop's record of what it sent.</summary>
    public const string CareLoop = "careloop";

    /// <summary>A text message channel: a delivery report or the patient's reply.</summary>
    public const string Sms = "sms";

    /// <summary>An email channel: a delivery report or the patient's reply.</summary>
    public const string Email = "email";

    /// <summary>What the timeline calls the case's own source system, used when an event has no origin.</summary>
    public const string Hospital = "hospital";
}
