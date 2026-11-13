namespace CareLoop.Api.Infrastructure.Notifications;

/// <summary>
/// Where <see cref="SimulatorNotificationProvider"/> delivers, bound to the
/// <c>Notifications:Simulator</c> configuration section.
/// </summary>
/// <remarks>
/// These addresses belong to the simulator provider alone. No other part of CareLoop knows the
/// simulators exist: choosing where notifications go is done by enabling a provider, and each
/// provider carries its own settings.
/// </remarks>
public sealed class SimulatorNotificationOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Notifications:Simulator";

    /// <summary>Base URL of the patient-channel simulator, which receives patient messages.</summary>
    public string? PatientChannelBaseUrl { get; set; }

    /// <summary>Base URL of the HIS simulator, whose staff inbox receives staff messages.</summary>
    public string? StaffInboxBaseUrl { get; set; }
}
