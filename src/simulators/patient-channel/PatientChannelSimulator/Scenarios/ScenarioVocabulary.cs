namespace PatientChannelSimulator.Scenarios;

/// <summary>
/// The scenario-file values this simulator acts on, as defined by <c>scenario.schema.json</c>.
/// </summary>
/// <remarks>
/// A scenario file is shared with the HIS simulator, which replays its steps. The patient channel
/// performs no steps: it reads only <c>patients[].behaviour</c> (<c>$defs/patientBehaviour</c>)
/// and these are the values that can appear there.
/// </remarks>
public static class ScenarioVocabulary
{
    /// <summary>Value of <c>behaviour.delivery</c>: report the notification as delivered.</summary>
    public const string DeliveryDeliver = "deliver";

    /// <summary>Value of <c>behaviour.delivery</c>: report the delivery as failed, with the failure reason.</summary>
    public const string DeliveryFail = "fail";

    /// <summary>Value of <c>behaviour.delivery</c>: accept the notification and report nothing.</summary>
    public const string DeliverySilent = "silent";

    /// <summary>Value of <c>behaviour.response</c>: the patient does nothing.</summary>
    public const string ResponseNone = "none";

    /// <summary>Value of <c>behaviour.response</c>: the patient confirms they saw the message.</summary>
    public const string ResponseAcknowledge = "acknowledge";

    /// <summary>Value of <c>behaviour.response</c>: the patient confirms they understood (teach-back).</summary>
    public const string ResponseUnderstand = "understand";

    /// <summary>Value of <c>behaviour.response</c>: the patient asks for a call.</summary>
    public const string ResponseCallMe = "call-me";
}
