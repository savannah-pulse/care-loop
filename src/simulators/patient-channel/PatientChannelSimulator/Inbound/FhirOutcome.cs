using Hl7.Fhir.Model;
using PatientChannelSimulator.Outbound;

namespace PatientChannelSimulator.Inbound;

/// <summary>
/// Builds the FHIR <c>OperationOutcome</c> answers of the simulator's FHIR endpoint.
/// </summary>
public static class FhirOutcome
{
    /// <summary>
    /// Builds an error answer.
    /// </summary>
    /// <param name="statusCode">The HTTP status code to answer with.</param>
    /// <param name="code">The FHIR issue type.</param>
    /// <param name="diagnostics">What went wrong, for the caller's log. Never clinical content.</param>
    /// <returns>A result carrying an <c>OperationOutcome</c> as <c>application/fhir+json</c>, tagged as simulated.</returns>
    public static IResult Error(int statusCode, OperationOutcome.IssueType code, string diagnostics)
    {
        var outcome = new OperationOutcome
        {
            Meta = new Meta
            {
                Tag = [new Coding(CommunicationCodes.SimulatedTagSystem, CommunicationCodes.SimulatedTagCode, CommunicationCodes.SimulatedTagDisplay)],
            },
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = code,
                    Diagnostics = diagnostics,
                },
            ],
        };

        return Results.Text(FhirJson.Serialize(outcome), FhirJson.MediaType, statusCode: statusCode);
    }
}
