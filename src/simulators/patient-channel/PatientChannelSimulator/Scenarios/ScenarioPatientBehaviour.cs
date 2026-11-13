namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// How a scenario patient reacts to notifications, as written in a scenario file
/// (<c>$defs/patientBehaviour</c> in the schema).
/// </summary>
/// <remarks>
/// This is the file shape, with values still as text. <see cref="ScenarioRunner"/> turns it into
/// a <see cref="PatientChannelSimulator.State.PatientBehaviour"/> when a scenario run starts.
/// </remarks>
public sealed record ScenarioPatientBehaviour
{
    /// <summary>One of <see cref="ScenarioVocabulary.DeliveryDeliver"/>, <see cref="ScenarioVocabulary.DeliveryFail"/> or <see cref="ScenarioVocabulary.DeliverySilent"/>.</summary>
    public required string Delivery { get; init; }

    /// <summary>Why delivery failed; present when <see cref="Delivery"/> is <see cref="ScenarioVocabulary.DeliveryFail"/>.</summary>
    public string? FailureReason { get; init; }

    /// <summary>One of the <c>Response</c> values in <see cref="ScenarioVocabulary"/>. Anything other than none requires delivery to be deliver.</summary>
    public required string Response { get; init; }

    /// <summary>ISO 8601 duration between delivery and the response, for example <c>PT1H</c>; absent means immediately.</summary>
    public string? RespondAfter { get; init; }

    /// <summary>Which received notification the patient responds to, counting from 1; absent means the first.</summary>
    public int? RespondToNotification { get; init; }
}
