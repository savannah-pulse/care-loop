namespace CareLoop.Domain.Pathways;

/// <summary>
/// Supplies pathway definitions.
/// </summary>
/// <remarks>
/// Stages, deadlines, escalation steps and closure evidence rules are configuration that a
/// clinical adviser must approve. Keeping them behind this interface means none of them is ever
/// hard-coded in a rule.
/// </remarks>
public interface IPathwayProvider
{
    /// <summary>
    /// Returns the definition of a pathway.
    /// </summary>
    /// <param name="pathwayId">Identifier of the pathway, for example <c>breast-screening</c>.</param>
    /// <returns>The pathway definition.</returns>
    /// <exception cref="KeyNotFoundException">No pathway with that identifier is configured.</exception>
    PathwayDefinition Get(string pathwayId);

    /// <summary>
    /// Lists every configured pathway.
    /// </summary>
    /// <returns>All pathway definitions known to the provider.</returns>
    IReadOnlyList<PathwayDefinition> GetAll();
}
