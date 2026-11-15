namespace HisSimulator.State;

/// <summary>
/// A synthetic patient known to the simulated HIS.
/// </summary>
/// <param name="SyntheticId">Synthetic patient identifier; always prefixed <c>SYN-</c>.</param>
/// <param name="DisplayName">Visibly synthetic display name; always starts with <c>Synthetic</c>.</param>
/// <param name="Language">BCP 47 language tag CareLoop uses to pick message templates.</param>
/// <param name="SecondaryIdentifier">Optional synthetic MRN; otherwise a deterministic synthetic MRN is generated.</param>
/// <param name="ContactAddress">Optional simulator-channel address; defaults to the synthetic patient identifier.</param>
public sealed record SimulatedPatient(string SyntheticId, string DisplayName, string Language,
    string? SecondaryIdentifier = null, string? ContactAddress = null)
{
    /// <summary>
    /// Always <see langword="true"/>: no real patient is ever represented.
    /// </summary>
    public bool Synthetic => true;
}
