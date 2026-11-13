namespace PatientChannelSimulator.Tests;

/// <summary>
/// Planned behaviour of the automatic responder: the simulator plays the patient by reacting to what it receives (WS8).
/// </summary>
public class AutomaticResponderTests
{
    [Fact(Skip = "WS8: not implemented")]
    public void Acknowledging_patient_acknowledges_a_delivered_notification() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Understanding_patient_sends_a_teach_back_response() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Call_me_patient_asks_for_a_call() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Silent_patient_never_responds() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Nothing_is_sent_when_no_notification_was_received() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Failed_delivery_gets_no_response() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Unreported_delivery_gets_no_response() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Response_waits_for_respond_after_on_the_simulator_clock() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Patient_who_responds_to_the_second_notification_ignores_the_first() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Resent_notification_does_not_trigger_a_second_response() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Automatic_response_is_recorded_as_an_automatic_reaction() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Response_rejected_by_CareLoop_is_recorded_as_failed() { }
}
