using System.Text;
using CareLoop.Api.Features.Fhir;
using CareLoop.Api.Features.Fhir.Inbound;
using CareLoop.Api.Features.Fhir.Outbound;
using CareLoop.Domain.Cases;
using CareLoop.Tests.Fakes;
using Hl7.Fhir.Model;
using CareLoop.Api.Infrastructure.Fhir;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using static CareLoop.Tests.Api.Fhir.FhirTestSupport;
using FhirTask = Hl7.Fhir.Model.Task;

namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// The FHIR endpoints parse, map and ingest; they translate the ingest result into a FHIR response.
/// </summary>
public sealed class FhirEndpointsTests
{
    private readonly IngestionHarness _harness = new();

    private static readonly IFhirInboundMapper[] Mappers =
    [
        new DiagnosticReportMapper(UnconfiguredClient()),
        new TaskMapper(),
        new ServiceRequestMapper(),
        new AppointmentMapper(),
        new ProcedureMapper(),
        new ProvenanceMapper(),
        new ConsentMapper(),
    ];

    private static readonly IOptions<ExternalSystemsOptions> Systems = Options.Create(new ExternalSystemsOptions
    {
        Systems = { [SourceSystemId] = new ExternalSystemEndpoint { BaseUrl = "http://his.invalid" } },
    });

    [Fact]
    public async System.Threading.Tasks.Task Post_of_a_new_resource_returns_201()
    {
        var result = await PostAsync("DiagnosticReport", Body(Sample<DiagnosticReport>("DiagnosticReport")));

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal("/fhir/DiagnosticReport/SYN-RESULT-0001", result.Location);
        Assert.IsType<DiagnosticReport>(result.Resource);
        var @case = _harness.TheCase();
        Assert.Equal(SourceSystemId, @case.SourceSystem);
        Assert.Equal("SYNTHETIC Patient One", @case.PatientDisplayName);
        Assert.Equal("SYN-MRN-0001", @case.PatientSecondaryIdentifier);
    }

    [Fact]
    public async System.Threading.Tasks.Task Resend_of_the_same_resource_version_returns_200_and_creates_nothing()
    {
        var body = Body(Sample<DiagnosticReport>("DiagnosticReport"));
        await PostAsync("DiagnosticReport", body);

        var resend = await PostAsync("DiagnosticReport", body);

        Assert.Equal(StatusCodes.Status200OK, resend.StatusCode);
        Assert.Null(resend.Location);
        Assert.Single(_harness.Repository.Cases);
        Assert.Single(_harness.Repository.Events);
    }

    [Fact]
    public async System.Threading.Tasks.Task Invalid_fhir_returns_400_with_an_OperationOutcome()
    {
        var notJson = await PostAsync("DiagnosticReport", "{ not json");
        var notFhir = await PostAsync("DiagnosticReport", """{ "resourceType": "DiagnosticReport", "status": "no-such-status" }""");

        AssertOutcome(notJson, StatusCodes.Status400BadRequest);
        AssertOutcome(notFhir, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async System.Threading.Tasks.Task Resource_from_an_unconfigured_source_system_returns_403_and_changes_nothing()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Meta!.Source = "SYN-UNKNOWN-SYSTEM";

        var result = await PostAsync("DiagnosticReport", FhirJsonCodec.Serialize(report));

        var outcome = AssertOutcome(result, StatusCodes.Status403Forbidden);
        Assert.Equal(OperationOutcome.IssueType.Forbidden, outcome.Issue[0].Code);
        Assert.Empty(_harness.Repository.Cases);
        Assert.Empty(_harness.Repository.Events);
    }

    [Fact]
    public async System.Threading.Tasks.Task Resource_type_without_a_mapper_returns_400()
    {
        var patient = Sample<Patient>("Patient");

        var result = await PostAsync("Patient", Body(patient));

        var outcome = AssertOutcome(result, StatusCodes.Status400BadRequest);
        Assert.Equal(OperationOutcome.IssueType.NotSupported, outcome.Issue[0].Code);
    }

    [Fact]
    public async System.Threading.Tasks.Task Missing_source_system_or_mismatched_type_returns_400()
    {
        var noSource = Sample<DiagnosticReport>("DiagnosticReport");

        AssertOutcome(await PostAsync("DiagnosticReport", FhirJsonCodec.Serialize(noSource)), StatusCodes.Status400BadRequest);
        AssertOutcome(await PostAsync("Procedure", Body(Sample<DiagnosticReport>("DiagnosticReport"))), StatusCodes.Status400BadRequest);
        Assert.Empty(_harness.Repository.Cases);
    }

    [Fact]
    public async System.Threading.Tasks.Task Resource_missing_a_mapped_field_returns_400()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Category.Clear();

        var result = await PostAsync("DiagnosticReport", Body(report));

        AssertOutcome(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async System.Threading.Tasks.Task Rule_rejection_returns_422_with_an_OperationOutcome()
    {
        await PostAsync("DiagnosticReport", Body(Sample<DiagnosticReport>("DiagnosticReport")));

        // An outcome before the clinical review: the transition rules refuse it.
        var result = await PostAsync("Procedure", Body(Sample<Procedure>("Procedure")));

        var outcome = AssertOutcome(result, StatusCodes.Status422UnprocessableEntity);
        Assert.Equal(OperationOutcome.IssueType.BusinessRule, outcome.Issue[0].Code);
    }

    [Fact]
    public async System.Threading.Tasks.Task Event_for_an_unknown_result_returns_400()
    {
        var result = await PostAsync("Task", Body(Sample<FhirTask>("Task")));

        AssertOutcome(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async System.Threading.Tasks.Task Resource_with_no_tracked_transition_returns_200()
    {
        var report = Sample<DiagnosticReport>("DiagnosticReport");
        report.Status = DiagnosticReport.DiagnosticReportStatus.Preliminary;

        var result = await PostAsync("DiagnosticReport", Body(report));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Empty(_harness.Repository.Cases);
    }

    [Fact]
    public async System.Threading.Tasks.Task Put_applies_the_new_state_and_returns_200()
    {
        await PostAsync("DiagnosticReport", Body(Sample<DiagnosticReport>("DiagnosticReport")));

        var result = await PutAsync("Task", "SYN-TASK-0001", Body(Sample<FhirTask>("Task")));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("PLACEHOLDER-URGENCY-A", _harness.TheCase().Urgency);
    }

    [Fact]
    public async System.Threading.Tasks.Task Put_with_a_mismatched_id_returns_400()
    {
        var result = await PutAsync("Task", "SYN-TASK-9999", Body(Sample<FhirTask>("Task")));

        AssertOutcome(result, StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async System.Threading.Tasks.Task Get_task_returns_the_case_as_a_task()
    {
        using var store = new FhirStore();
        var @case = TestData.NewCase(store.Clock.UtcNow);
        await store.SaveAsync(@case);
        await using var db = store.NewContext();

        var result = Assert.IsType<FhirResourceResult>(await FhirEndpoints.GetTask(@case.Id.ToString(), db, new CaseTaskMapper(), CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        var task = Assert.IsType<FhirTask>(result.Resource);
        Assert.Equal(@case.Id.ToString("D"), task.Id);
        Assert.Equal(CaseStage.Received, StatusMap.ToStage(task.BusinessStatus));
    }

    [Fact]
    public async System.Threading.Tasks.Task Get_task_for_an_unknown_case_returns_404()
    {
        using var store = new FhirStore();
        await using var db = store.NewContext();

        var unknown = Assert.IsType<FhirResourceResult>(await FhirEndpoints.GetTask(Guid.NewGuid().ToString(), db, new CaseTaskMapper(), CancellationToken.None));
        var malformed = Assert.IsType<FhirResourceResult>(await FhirEndpoints.GetTask("not-a-case", db, new CaseTaskMapper(), CancellationToken.None));

        AssertOutcome(unknown, StatusCodes.Status404NotFound);
        AssertOutcome(malformed, StatusCodes.Status404NotFound);
    }

    [Fact]
    public void Metadata_lists_every_supported_resource_type()
    {
        var result = Assert.IsType<FhirResourceResult>(GetCapabilityStatement.Handle(Mappers, new FakeClock()));

        var statement = Assert.IsType<CapabilityStatement>(result.Resource);
        var resources = statement.Rest.Single().Resource.ToDictionary(r => r.Type!, r => r.Interaction.Select(i => i.Code!.Value).ToList());
        Assert.Equal(
            ["Appointment", "Consent", "DiagnosticReport", "Procedure", "Provenance", "ServiceRequest", "Task"],
            resources.Keys.Order());
        Assert.Contains(CapabilityStatement.TypeRestfulInteraction.Create, resources["DiagnosticReport"]);
        Assert.Contains(CapabilityStatement.TypeRestfulInteraction.Update, resources["DiagnosticReport"]);
        Assert.Contains(CapabilityStatement.TypeRestfulInteraction.Read, resources["Task"]);
        Assert.Contains(FhirEndpoints.FhirJson, statement.Format);
    }

    [Fact]
    public async System.Threading.Tasks.Task Responses_are_written_as_fhir_json()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var result = await PostAsync("DiagnosticReport", Body(Sample<DiagnosticReport>("DiagnosticReport")));

        await result.ExecuteAsync(context);

        Assert.StartsWith(FhirEndpoints.FhirJson, context.Response.ContentType);
        Assert.Equal("/fhir/DiagnosticReport/SYN-RESULT-0001", context.Response.Headers.Location.ToString());
        context.Response.Body.Position = 0;
        Assert.IsType<DiagnosticReport>(FhirJsonCodec.Deserialize(await new StreamReader(context.Response.Body).ReadToEndAsync()));
    }

    private static string Body(Resource resource)
    {
        resource.Meta ??= new Meta();
        resource.Meta.Source = SourceSystemId;
        return FhirJsonCodec.Serialize(resource);
    }

    private static OperationOutcome AssertOutcome(FhirResourceResult result, int status)
    {
        Assert.Equal(status, result.StatusCode);
        var outcome = Assert.IsType<OperationOutcome>(result.Resource);
        Assert.NotEmpty(outcome.Issue);
        return outcome;
    }

    private async System.Threading.Tasks.Task<FhirResourceResult> PostAsync(string type, string body) =>
        Assert.IsType<FhirResourceResult>(await FhirEndpoints.CreateResource(type, Request(body), Mappers, _harness.Ingestor, Systems, CancellationToken.None));

    private async System.Threading.Tasks.Task<FhirResourceResult> PutAsync(string type, string id, string body) =>
        Assert.IsType<FhirResourceResult>(await FhirEndpoints.UpdateResource(type, id, Request(body), Mappers, _harness.Ingestor, Systems, CancellationToken.None));

    private static HttpRequest Request(string body)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = FhirEndpoints.FhirJson;
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return context.Request;
    }
}
