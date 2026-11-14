namespace PatientChannelSimulator.Outbound;

/// <summary>
/// Thrown when the simulator cannot call CareLoop back because it has no signing secret.
/// </summary>
/// <remarks>
/// CareLoop refuses an unsigned callback. Rather than send something that is certain to be
/// refused, the simulator sends nothing and says which setting is missing.
/// </remarks>
public sealed class CareLoopNotConfiguredException : InvalidOperationException
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="message">Which setting is missing and what that means.</param>
    public CareLoopNotConfiguredException(string message)
        : base(message)
    {
    }
}
