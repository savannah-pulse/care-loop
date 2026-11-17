namespace MessagingSimulator.Tests;

/// <summary>A throw-away scenario directory for tests that need their own files.</summary>
internal sealed class TempScenarios : IDisposable
{
    public TempScenarios()
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "careloop-ws8-" + Guid.NewGuid().ToString("N"));
        Path = System.IO.Path.Combine(Root, "scenarios");
        Directory.CreateDirectory(Path);
    }

    /// <summary>The parent folder, used as a content root.</summary>
    public string Root { get; }

    /// <summary>The scenario folder itself.</summary>
    public string Path { get; }

    public TempScenarios Add(string fileName, string json)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, fileName), json);
        return this;
    }

    /// <summary>Adds a one-patient scenario whose patient has the given behaviour JSON.</summary>
    public TempScenarios AddScenario(string name, string behaviourJson, string patientId = "SYN-PT-0001", bool synthetic = true, string? declaredName = null) =>
        Add(name + ".json", $$"""
            {
              // Comments and trailing commas are tolerated.
              "name": "{{declaredName ?? name}}",
              "synthetic": {{(synthetic ? "true" : "false")}},
              "description": "SIMULATED test scenario.",
              "pathwayId": "PLACEHOLDER-PATHWAY",
              "patients": [
                { "ref": "patient-1", "syntheticId": "{{patientId}}", "displayName": "Synthetic Patient", "language": "en", "behaviour": {{behaviourJson}} },
              ],
              "steps": [],
            }
            """);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temp folder does not affect any test.
        }
    }
}
