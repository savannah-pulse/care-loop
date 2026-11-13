using System.Text.Json;
using System.Text.Json.Serialization;
using CareLoop.Domain.Cases;
using CareLoop.Domain.Pathways;
using Microsoft.Extensions.Options;

namespace CareLoop.Api.Infrastructure.Pathways;

/// <summary>
/// Loads pathway definitions from the JSON files in the folder named by <see cref="PathwayOptions.Directory"/>.
/// </summary>
/// <remarks>
/// Reads every <c>*.json</c> file directly in the folder (not its subfolders) on first use, deserializes
/// it to a <see cref="PathwayDefinition"/> (camelCase names, enums as strings, durations as
/// <c>d.hh:mm:ss</c>) and caches it for the lifetime of the provider. Fails loudly on a malformed
/// file, on a duplicate pathway identifier, or on a definition missing a rule for any stage or
/// closure reason or a deadline for any next action it names, because a silently incomplete pathway
/// would let cases stall without a deadline. A failed load is not cached, so a corrected file is
/// picked up on the next call.
/// </remarks>
public sealed class JsonPathwayProvider : IPathwayProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private readonly IHostEnvironment _environment;
    private readonly IOptions<PathwayOptions> _options;
    private readonly Lock _gate = new();
    private IReadOnlyDictionary<string, PathwayDefinition>? _definitions;

    /// <summary>
    /// Creates the provider. Nothing is loaded until the first call.
    /// </summary>
    /// <param name="environment">Supplies the application's content root.</param>
    /// <param name="options">Names the folder to load from, relative to the content root.</param>
    public JsonPathwayProvider(IHostEnvironment environment, IOptions<PathwayOptions> options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        _environment = environment;
        _options = options;
    }

    /// <summary>
    /// The absolute folder the definitions are read from.
    /// </summary>
    public string Directory =>
        Path.IsPathRooted(_options.Value.Directory)
            ? _options.Value.Directory
            : Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _options.Value.Directory));

    /// <inheritdoc/>
    public PathwayDefinition Get(string pathwayId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathwayId);

        var definitions = Load();
        if (!definitions.TryGetValue(pathwayId, out var definition))
        {
            throw new KeyNotFoundException(
                $"No pathway '{pathwayId}' is configured in '{Directory}'. Known pathways: {string.Join(", ", definitions.Keys.Order())}.");
        }

        return definition;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PathwayDefinition> GetAll() =>
        Load().Values.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();

    /// <summary>
    /// Reads, validates and caches every definition in the folder, once.
    /// </summary>
    /// <returns>The definitions keyed by identifier.</returns>
    /// <exception cref="DirectoryNotFoundException">The configured folder does not exist.</exception>
    /// <exception cref="InvalidOperationException">The folder holds no definition, a file is malformed, two files share an identifier, or a definition is incomplete.</exception>
    private IReadOnlyDictionary<string, PathwayDefinition> Load()
    {
        if (_definitions is not null)
        {
            return _definitions;
        }

        lock (_gate)
        {
            if (_definitions is not null)
            {
                return _definitions;
            }

            var directory = Directory;
            if (!System.IO.Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"Pathway folder '{directory}' does not exist.");
            }

            var files = System.IO.Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .ToList();

            if (files.Count == 0)
            {
                throw new InvalidOperationException($"Pathway folder '{directory}' holds no *.json definition.");
            }

            var loaded = new Dictionary<string, PathwayDefinition>(StringComparer.Ordinal);
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var file in files)
            {
                var definition = ReadFile(file);
                Validate(definition, file);

                if (sources.TryGetValue(definition.Id, out var other))
                {
                    throw new InvalidOperationException(
                        $"Pathway '{definition.Id}' is defined twice: in '{other}' and in '{file}'.");
                }

                sources[definition.Id] = file;
                loaded[definition.Id] = definition;
            }

            _definitions = loaded;
            return _definitions;
        }
    }

    private static PathwayDefinition ReadFile(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            return JsonSerializer.Deserialize<PathwayDefinition>(stream, SerializerOptions)
                ?? throw new InvalidOperationException($"Pathway file '{file}' is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Pathway file '{file}' is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Checks that a definition is complete enough that no case following it can stall.
    /// </summary>
    private static void Validate(PathwayDefinition definition, string file)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            problems.Add("has no id");
        }

        if (string.IsNullOrWhiteSpace(definition.UnassignedQueueRole))
        {
            problems.Add("names no unassignedQueueRole");
        }

        var definedStages = definition.Stages.Select(s => s.Stage).ToHashSet();
        foreach (var stage in Enum.GetValues<CaseStage>().Where(s => !definedStages.Contains(s)))
        {
            problems.Add($"has no rule for stage {stage}");
        }

        foreach (var duplicate in definition.Stages.GroupBy(s => s.Stage).Where(g => g.Count() > 1))
        {
            problems.Add($"defines stage {duplicate.Key} more than once");
        }

        var definedReasons = definition.ClosureRules.Select(r => r.Reason).ToHashSet();
        foreach (var reason in Enum.GetValues<ClosureReason>().Where(r => !definedReasons.Contains(r)))
        {
            problems.Add($"has no closure rule for reason {reason}");
        }

        foreach (var duplicate in definition.ClosureRules.GroupBy(r => r.Reason).Where(g => g.Count() > 1))
        {
            problems.Add($"defines closure reason {duplicate.Key} more than once");
        }

        foreach (var rule in definition.ClosureRules.Where(r => r.AuthorizedRoles.Count == 0))
        {
            problems.Add($"authorizes nobody to close for reason {rule.Reason}");
        }

        var deadlineActions = definition.Deadlines
            .Select(d => d.NextAction)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var namedActions = definition.Stages
            .Select(s => s.NextActionOnEntry)
            .Concat(definition.Tracks.Select(t => t.NextActionOnException))
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var action in namedActions.Where(a => !deadlineActions.Contains(a)))
        {
            problems.Add($"names next action '{action}' but defines no deadline for it");
        }

        foreach (var deadline in definition.Deadlines.Where(d => d.Duration <= TimeSpan.Zero))
        {
            problems.Add($"gives next action '{deadline.NextAction}' a non-positive duration");
        }

        foreach (var deadline in definition.Deadlines.Where(d => d.UrgencyCode is not null && !definition.UrgencyCodes.Contains(d.UrgencyCode, StringComparer.Ordinal)))
        {
            problems.Add($"gives next action '{deadline.NextAction}' a deadline for unknown urgency code '{deadline.UrgencyCode}'");
        }

        if (definition.EscalationLadder.Count == 0)
        {
            problems.Add("has an empty escalation ladder");
        }

        foreach (var duplicate in definition.EscalationLadder.GroupBy(s => s.Level).Where(g => g.Count() > 1))
        {
            problems.Add($"defines escalation level {duplicate.Key} more than once");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Pathway file '{file}' is incomplete: it {string.Join("; it ", problems)}.");
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
