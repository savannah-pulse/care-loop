using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// Every sample resource must be valid FHIR R4 and visibly synthetic.
/// </summary>
public sealed class FhirSampleFilesTests
{
    private static readonly JsonSerializerOptions FhirOptions = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

    public static TheoryData<string> SampleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(SamplesDirectory, "*.json").Order())
        {
            data.Add(Path.GetFileName(file));
        }

        return data;
    }

    private static string SamplesDirectory => Path.Combine(AppContext.BaseDirectory, "Api", "Fhir", "Samples");

    [Fact]
    public void There_is_one_sample_per_boundary_resource()
    {
        string[] expected =
        [
            "Appointment", "Communication", "CommunicationRequest", "DiagnosticReport", "DocumentReference",
            "Observation", "Procedure", "Provenance", "ServiceRequest", "Task",
        ];

        var actual = Directory.GetFiles(SamplesDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).Order();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void Sample_parses_as_fhir_r4_and_is_marked_synthetic(string fileName)
    {
        var json = File.ReadAllText(Path.Combine(SamplesDirectory, fileName));

        var resource = JsonSerializer.Deserialize<Resource>(json, FhirOptions);

        Assert.NotNull(resource);
        Assert.Equal(Path.GetFileNameWithoutExtension(fileName), resource.TypeName);
        Assert.StartsWith("SYN-", resource.Id);
        Assert.NotNull(resource.Meta);
        Assert.Contains(resource.Meta.Tag, tag => tag.Code == "simulated");
    }
}
