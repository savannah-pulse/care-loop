namespace CareLoop.Tests.Api;

/// <summary>
/// One intake pipeline: validate, deduplicate, append, apply, audit.
/// </summary>
public sealed class EventIngestorTests
{
    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Replaying_the_same_source_event_produces_one_case_and_one_event()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Duplicate_event_returns_duplicate_and_changes_nothing()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Screening_result_opens_a_case()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Event_for_an_unknown_result_is_invalid()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Envelope_with_mismatched_payload_type_is_invalid()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Rejected_event_is_stored_and_audited_but_leaves_the_case_unchanged()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Applied_event_writes_an_audit_record_with_prior_and_new_projection()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Domain_events_are_written_to_the_outbox_in_the_same_save()
    {
    }

    // TODO(WS-2): implement this test.
    [Fact(Skip = "WS2: not implemented")]
    public void Received_timestamp_comes_from_the_clock()
    {
    }
}
