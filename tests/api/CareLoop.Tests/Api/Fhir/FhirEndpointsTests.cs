namespace CareLoop.Tests.Api.Fhir;

/// <summary>
/// The FHIR boundary parses, maps and hands over; it holds no business logic.
/// </summary>
public sealed class FhirEndpointsTests
{
    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Post_of_a_new_resource_returns_201()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Resend_of_the_same_resource_version_returns_200_and_creates_nothing()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Invalid_fhir_returns_400_with_an_OperationOutcome()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Resource_type_without_a_mapper_returns_400()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Rule_rejection_returns_422_with_an_OperationOutcome()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Put_with_a_mismatched_id_returns_400()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Get_task_returns_the_case_as_a_task()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Get_task_for_an_unknown_case_returns_404()
    {
    }

    // TODO(WS-3): implement this test.
    [Fact(Skip = "WS3: not implemented")]
    public void Metadata_lists_every_supported_resource_type()
    {
    }
}
