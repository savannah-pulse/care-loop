using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// How the simulated patient channel presents what it receives, bound from the <c>PatientChannel</c> section.
/// </summary>
public sealed class PatientChannelOptions
{
    /// <summary>Name of the configuration section these options bind to.</summary>
    public const string SectionName = "PatientChannel";

    /// <summary>
    /// The inbox a patient message is shown in when its inbox address does not name a channel.
    /// </summary>
    /// <remarks>
    /// CareLoop's request carries no channel. A caregiver's copy always goes to
    /// <see cref="InboxChannel.Email"/>; a patient message goes to the channel its inbox address
    /// names (see <see cref="CommunicationRequestMapper"/>) and otherwise to this one.
    /// </remarks>
    public InboxChannel DefaultChannel { get; set; } = InboxChannel.App;
}
