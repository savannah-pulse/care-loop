namespace PatientChannelSimulator.Outbound;

/// <summary>
/// The responses a simulated patient can give to an inbox message.
/// </summary>
/// <remarks>
/// Each kind maps to its own <c>Communication.category</c> (see <see cref="CommunicationCodes"/>)
/// and its own builder. A delivery report is deliberately not a member: delivery is not a patient response.
/// </remarks>
public enum PatientResponseKind
{
    /// <summary>The patient confirms they received the message.</summary>
    Acknowledgement,

    /// <summary>The patient confirms they understood it (teach-back).</summary>
    TeachBack,

    /// <summary>The patient asks to be called; CareLoop raises a next action for the current owner.</summary>
    CallMe,
}
