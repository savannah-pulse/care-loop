using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>
/// Planned behaviour of per-patient behaviours and the delivery outcome they produce (WS8).
/// </summary>
public class PatientBehaviourTests
{
    [Fact]
    public void Default_behaviour_is_deliver_and_do_not_respond()
    {
        var behaviour = new SimulatorStore().GetDefaultBehaviour();

        Assert.Equal(DeliveryBehaviour.Deliver, behaviour.Delivery);
        Assert.Equal(PatientResponseBehaviour.None, behaviour.Response);
        Assert.Null(behaviour.Confidence);
        Assert.True(behaviour.Simulated);
    }

    [Fact]
    public void Patient_without_a_behaviour_gets_the_default()
    {
        var store = new SimulatorStore();
        var silent = new PatientBehaviour(DeliveryBehaviour.StaySilent, PatientResponseBehaviour.None);
        store.SetDefaultBehaviour(silent);
        store.SetBehaviour(new PatientBehaviourAssignment("SYN-PT-0002", new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.Acknowledge), null));

        Assert.Same(silent, store.GetBehaviour(TestData.PatientId));
        Assert.Equal(PatientResponseBehaviour.Acknowledge, store.GetBehaviour("SYN-PT-0002").Response);
    }

    [Fact(Skip = "WS8: not implemented")]
    public void Deliver_sends_a_completed_communication_and_marks_the_message_delivered() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Fail_sends_a_not_done_communication_with_the_behaviour_reason() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Stay_silent_sends_nothing_and_marks_the_message_silent() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Two_patients_with_different_behaviours_get_different_outcomes() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Changing_a_behaviour_does_not_affect_messages_already_in_the_inbox() { }

    [Fact]
    public void Removing_a_behaviour_restores_the_default()
    {
        var store = new SimulatorStore();
        store.SetBehaviour(new PatientBehaviourAssignment(TestData.PatientId, new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: unreachable"), "silent-patient"));

        Assert.Equal(TestData.PatientId, Assert.Single(store.GetBehaviours()).PatientSyntheticId);
        Assert.True(store.RemoveBehaviour(TestData.PatientId));
        Assert.False(store.RemoveBehaviour(TestData.PatientId));

        Assert.Empty(store.GetBehaviours());
        Assert.Equal(store.GetDefaultBehaviour(), store.GetBehaviour(TestData.PatientId));
    }

    [Fact(Skip = "WS8: not implemented")]
    public void Fail_without_a_reason_is_rejected() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Response_without_delivery_is_rejected() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Behaviour_for_a_non_synthetic_patient_identifier_is_rejected() { }

    [Fact(Skip = "WS8: not implemented")]
    public void Every_delivery_outcome_is_recorded_as_a_reaction() { }
}
