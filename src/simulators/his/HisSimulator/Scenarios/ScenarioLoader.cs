using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HisSimulator.Outbound;

namespace HisSimulator.Scenarios;

/// <summary>
/// Reads scenario files from the directory configured in <see cref="ScenarioOptions.Directory"/>.
/// </summary>
/// <remarks>
/// The scenario files are shared with the patient-channel simulator and CareLoop's journey tests,
/// so this class only reads them. Files are deserialized with <see cref="ScenarioJson.Options"/>;
/// <c>scenario.schema.json</c> itself is not a scenario and must be ignored.
/// </remarks>
public sealed class ScenarioLoader
{
    private readonly IOptions<ScenarioOptions> _options;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Creates the loader.
    /// </summary>
    /// <param name="options">Supplies the scenarios directory.</param>
    /// <param name="environment">Supplies the content root that a relative directory is resolved against.</param>
    public ScenarioLoader(IOptions<ScenarioOptions> options, IHostEnvironment environment)
    {
        _options = options;
        _environment = environment;
    }

    /// <summary>
    /// Loads every scenario in the configured directory.
    /// </summary>
    /// <param name="cancellationToken">Cancels the file reads.</param>
    /// <returns>All scenarios ordered by name, both step-based and cohort.</returns>
    /// <exception cref="DirectoryNotFoundException">The configured directory does not exist.</exception>
    /// <exception cref="InvalidDataException">
    /// A file is not valid against the scenario schema, including <c>synthetic</c> not being <see langword="true"/>,
    /// a name that differs from the file name, a synthetic identifier without the <c>SYN-</c> prefix,
    /// or a file that is neither step-based nor a cohort.
    /// </exception>
    public async Task<IReadOnlyList<ScenarioDefinition>> LoadAllAsync(CancellationToken cancellationToken)
    {
        var results = new List<ScenarioDefinition>();
        foreach (var file in System.IO.Directory.EnumerateFiles(DirectoryPath(), "*.json").Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Path.GetFileName(file) == "scenario.schema.json") continue;
            results.Add(await ReadAsync(file, cancellationToken));
        }
        return results.OrderBy(s => s.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Loads one scenario by name.
    /// </summary>
    /// <param name="name">Scenario key; equals the file name without extension (lower-case letters, digits and hyphens).</param>
    /// <param name="cancellationToken">Cancels the file read.</param>
    /// <returns>The validated scenario.</returns>
    /// <exception cref="ScenarioNotFoundException">No file exists for <paramref name="name"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> does not match the schema's name pattern. Rejecting it prevents a route value
    /// from being used to read a file outside the scenarios directory.
    /// </exception>
    /// <exception cref="InvalidDataException">The file is not valid against the scenario schema.</exception>
    public async Task<ScenarioDefinition> LoadAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || !Regex.IsMatch(name, "^[a-z0-9-]+$"))
            throw new ArgumentException("A scenario name may contain lower-case letters, digits and hyphens only.");
        var file = Path.Combine(DirectoryPath(), name + ".json");
        if (!File.Exists(file)) throw new ScenarioNotFoundException(name);
        return await ReadAsync(file, cancellationToken);
    }

    private string DirectoryPath() => Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.Value.Directory));

    private static async Task<ScenarioDefinition> ReadAsync(string file, CancellationToken ct)
    {
        ScenarioDefinition scenario;
        try
        {
            scenario = JsonSerializer.Deserialize<ScenarioDefinition>(await File.ReadAllTextAsync(file, ct), ScenarioJson.Options)
                ?? throw new InvalidDataException("The scenario body is empty.");
        }
        catch (JsonException ex) { throw new InvalidDataException("The scenario is not valid JSON for the scenario contract.", ex); }
        if (!scenario.Synthetic || scenario.Name != Path.GetFileNameWithoutExtension(file)
            || !Regex.IsMatch(scenario.Name ?? "", "^[a-z0-9-]+$")
            || string.IsNullOrWhiteSpace(scenario.Description) || string.IsNullOrWhiteSpace(scenario.PathwayId))
            throw new InvalidDataException("A scenario must be synthetic, named after its file and have a description/pathway.");
        if (scenario.Cohort is { } cohort)
        {
            if (scenario.Steps is not null || scenario.Patients is not null || cohort.Size < 1 || cohort.Mix is not { Count: > 0 }
                || cohort.Mix.Any(m => string.IsNullOrWhiteSpace(m.Scenario) || !double.IsFinite(m.Weight) || m.Weight <= 0))
                throw new InvalidDataException("A cohort must have a positive size and weighted mix, and no patient/step lists.");
            return scenario;
        }
        if (scenario.Patients is not { Count: > 0 } patients || scenario.Steps is not { Count: > 0 } steps)
            throw new InvalidDataException("A step-based scenario must contain patients and steps.");
        if (patients.Select(p => p.Ref).Distinct(StringComparer.Ordinal).Count() != patients.Count
            || patients.Select(p => p.SyntheticId).Distinct(StringComparer.Ordinal).Count() != patients.Count
            || steps.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != steps.Count)
            throw new InvalidDataException("Patient references/identifiers and step identifiers must be unique.");
        foreach (var patient in patients)
        {
            if (string.IsNullOrWhiteSpace(patient.Ref) || !FhirSupport.SyntheticId(patient.SyntheticId)
                || patient.DisplayName?.StartsWith("Synthetic ", StringComparison.Ordinal) != true
                || !Regex.IsMatch(patient.Language ?? "", "^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$"))
                throw new InvalidDataException("Each patient needs a synthetic identifier/name and valid language.");
            ValidateBehaviour(patient.Behaviour);
        }
        var knownCases = new HashSet<string>(StringComparer.Ordinal);
        var knownSteps = new Dictionary<string, (string CaseRef, bool IsOutcome)>(StringComparer.Ordinal);
        var previousOffset = TimeSpan.Zero;
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id) || string.IsNullOrWhiteSpace(step.CaseRef)
                || !Enum.IsDefined(step.Actor) || !Enum.IsDefined(step.Action))
                throw new InvalidDataException("A step needs a stable id, case reference and known actor/action.");
            TimeSpan offset;
            try { offset = XmlConvert.ToTimeSpan(step.At); }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            { throw new InvalidDataException("The step offset must be an ISO 8601 duration.", ex); }
            if (offset < previousOffset) throw new InvalidDataException("Steps must have nonnegative, nondecreasing offsets.");
            previousOffset = offset;
            if (step.Performer is { } performer && (!FhirSupport.SyntheticId(performer.SyntheticId)
                    || performer.Facility is not null && !FhirSupport.SyntheticId(performer.Facility)
                    || string.IsNullOrWhiteSpace(performer.Role)))
                throw new InvalidDataException("A scenario performer must have a synthetic identifier/facility and a role.");
            if (step.Action == ScenarioAction.ScreeningResult)
            {
                if (!patients.Any(p => p.Ref == step.PatientRef) || !knownCases.Add(step.CaseRef))
                    throw new InvalidDataException("A screening step needs a known patient and a new case reference.");
            }
            else if (!knownCases.Contains(step.CaseRef))
                throw new InvalidDataException("A follow-up step must refer to an earlier screening case.");
            if (step.Params?.TryGetValue("evidenceFromStep", out var evidence) == true
                && (evidence.ValueKind != JsonValueKind.String
                    || !knownSteps.TryGetValue(evidence.GetString() ?? "", out var prior)
                    || prior.CaseRef != step.CaseRef || !prior.IsOutcome))
                throw new InvalidDataException("Evidence must refer to an earlier outcome for the same case.");
            knownSteps.Add(step.Id, (step.CaseRef, step.Action == ScenarioAction.Outcome));
        }
        return scenario;
    }

    private static void ValidateBehaviour(JsonElement? behaviour)
    {
        if (behaviour is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty("delivery", out var delivery) || delivery.ValueKind != JsonValueKind.String
            || !value.TryGetProperty("response", out var response) || response.ValueKind != JsonValueKind.String
            || delivery.GetString() is not ("deliver" or "fail" or "silent")
            || response.GetString() is not ("none" or "acknowledge" or "understand" or "call-me"))
            throw new InvalidDataException("A patient needs a valid delivery/response behaviour.");
        if (value.EnumerateObject().Any(p => p.Name is not ("delivery" or "response" or "failureReason" or "respondAfter" or "respondToNotification")))
            throw new InvalidDataException("The patient behaviour contains an unknown field.");
        if (delivery.GetString() is "fail" or "silent" && response.GetString() != "none")
            throw new InvalidDataException("A failed or silent delivery cannot have a patient response.");
        if (delivery.GetString() == "fail" && (!value.TryGetProperty("failureReason", out var reason)
                || reason.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(reason.GetString())))
            throw new InvalidDataException("Failed delivery needs a documented reason.");
        if (value.TryGetProperty("respondToNotification", out var index)
            && (index.ValueKind != JsonValueKind.Number || !index.TryGetInt32(out var n) || n < 1))
            throw new InvalidDataException("The response notification index must be a positive integer.");
        if (value.TryGetProperty("respondAfter", out var delay))
        {
            try
            {
                if (delay.ValueKind != JsonValueKind.String || XmlConvert.ToTimeSpan(delay.GetString()!) < TimeSpan.Zero)
                    throw new InvalidDataException("The patient response delay must be nonnegative.");
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            { throw new InvalidDataException("The patient response delay is invalid.", ex); }
        }
    }
}
