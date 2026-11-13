namespace PatientChannelSimulator.Tests;

/// <summary>Planned WS8 behaviours: inbox state.</summary>
public class InboxStateTests
{
    [Fact(Skip = "WS8: not implemented")]
    public void Inbox_lists_messages_newest_first() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Adding_a_duplicate_message_id_is_rejected() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Acknowledging_updates_the_response_state_only_after_CareLoop_accepts() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Understanding_updates_the_response_state_to_understood() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Call_me_updates_the_response_state_to_call_requested() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Responding_to_an_unknown_message_returns_not_found() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Delivered_message_stays_unacknowledged_until_the_patient_acts() { }
}
