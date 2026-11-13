namespace HisSimulator.Tests.Outbound;

/// <summary>Planned behaviour of <c>OutcomeBuilder</c> (Procedure or follow-up DiagnosticReport).</summary>
public sealed class OutcomeBuilderTests
{
    [Fact(Skip = "WS7: not implemented")]
    public void BuildProcedure_returns_completed_procedure_with_placeholder_evidence_kind()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void BuildFollowUpReport_references_the_original_screening_report()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void BuildFollowUpReport_is_distinguishable_from_a_new_screening_result()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Built_outcome_has_deterministic_id_and_simulated_tag()
    {
    }

    [Fact(Skip = "WS7: not implemented")]
    public void Build_rejects_a_request_without_evidence_kind()
    {
    }
}
