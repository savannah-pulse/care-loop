using System.Text.Json.Nodes;

namespace HisSimulator.Tests;

internal sealed class ScenarioFolder : IDisposable
{
    internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "careloop-his-" + Guid.NewGuid().ToString("N"));
    internal JsonObject Scenario { get; } = JsonNode.Parse("""
    {"name":"test","description":"Synthetic replay","synthetic":true,"pathwayId":"breast-screening",
     "patients":[{"ref":"p1","syntheticId":"SYN-PT-1","displayName":"Synthetic Patient 1","language":"en","behaviour":{"delivery":"deliver","response":"none"}}],
     "steps":[
      {"id":"s1","at":"PT0S","actor":"his","action":"screening-result","caseRef":"c1","patientRef":"p1","params":{"facility":"SYN-FAC-A","resultCategory":"PLACEHOLDER-RESULT-ACTIONABLE"}},
      {"id":"s2","at":"PT0S","actor":"his","action":"clinical-review","caseRef":"c1","performer":{"syntheticId":"SYN-STAFF-1","role":"clinician","facility":"SYN-FAC-A"},"params":{"urgency":"PLACEHOLDER-URGENCY-A"}}
     ]}
    """)!.AsObject();
    internal ScenarioFolder() { Directory.CreateDirectory(Path); Save(); }
    internal void Save() => File.WriteAllText(System.IO.Path.Combine(Path, "test.json"), Scenario.ToJsonString());
    public void Dispose() => Directory.Delete(Path, true);
}
