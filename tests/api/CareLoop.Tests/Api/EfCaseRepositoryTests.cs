namespace CareLoop.Tests.Api;

/// <summary>
/// The single write path stores everything in one transaction.
/// </summary>
public sealed class EfCaseRepositoryTests
{
    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Save_persists_event_case_audit_and_outbox_together()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Failure_in_any_part_stores_nothing()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Duplicate_source_event_is_refused_by_the_unique_index()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Stale_case_version_is_refused()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Case_round_trips_with_tracks_escalations_and_closure()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void One_outbox_delivery_row_is_created_per_subscriber()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Find_by_source_returns_the_case_for_a_result()
    {
    }
}
