using System.Net;
using System.Net.Http.Json;
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

    [Fact]
    public async Task Deliver_sends_a_completed_communication_and_marks_the_message_delivered()
    {
        using var simulator = new SimulatorFactory();

        await simulator.NotifyAsync();

        var report = Assert.Single(simulator.CareLoop.Callbacks);
        Assert.Equal("delivery-status", report.Category);
        Assert.Equal("completed", report.Status);
        Assert.Equal(DeliveryState.Delivered, (await simulator.InboxMessageAsync()).Delivery);
    }

    [Fact]
    public async Task Fail_sends_a_not_done_communication_with_the_behaviour_reason()
    {
        using var simulator = new SimulatorFactory();
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: handset unreachable"));

        await simulator.NotifyAsync();

        var report = Assert.Single(simulator.CareLoop.Callbacks);
        Assert.Equal("delivery-status", report.Category);
        Assert.Equal("not-done", report.Status);
        Assert.Equal("SIMULATED: handset unreachable", report.Json.GetProperty("statusReason").GetProperty("text").GetString());
        var message = await simulator.InboxMessageAsync();
        Assert.Equal(DeliveryState.Failed, message.Delivery);
        Assert.Equal("SIMULATED: handset unreachable", message.DeliveryFailureReason);
    }

    [Fact]
    public async Task Stay_silent_sends_nothing_and_marks_the_message_silent()
    {
        using var simulator = new SimulatorFactory();
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.StaySilent, PatientResponseBehaviour.None));

        var response = await simulator.NotifyAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(simulator.CareLoop.Calls);
        Assert.Equal(DeliveryState.Silent, (await simulator.InboxMessageAsync()).Delivery);
    }

    [Fact]
    public async Task Two_patients_with_different_behaviours_get_different_outcomes()
    {
        using var simulator = new SimulatorFactory();
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Deliver, PatientResponseBehaviour.Acknowledge), "SYN-PT-0001");
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: unreachable"), "SYN-PT-0002");

        await simulator.NotifyAsync(requestId: "one", patientId: "SYN-PT-0001");
        await simulator.NotifyAsync(requestId: "two", patientId: "SYN-PT-0002");
        await simulator.NotifyAsync(requestId: "three", patientId: "SYN-PT-0003");

        var one = await simulator.InboxMessageAsync("one");
        var two = await simulator.InboxMessageAsync("two");
        var three = await simulator.InboxMessageAsync("three");
        Assert.Equal((DeliveryState.Delivered, ResponseState.Acknowledged), (one.Delivery, one.Response));
        Assert.Equal((DeliveryState.Failed, ResponseState.None), (two.Delivery, two.Response));
        Assert.Equal((DeliveryState.Delivered, ResponseState.None), (three.Delivery, three.Response));
    }

    [Fact]
    public async Task Changing_a_behaviour_does_not_affect_messages_already_in_the_inbox()
    {
        using var simulator = new SimulatorFactory();
        await simulator.NotifyAsync(requestId: "before");

        var put = await simulator.CreateClient().PutAsJsonAsync(
            $"/api/patient-behaviours/{TestData.PatientId}",
            new { delivery = "Fail", response = "None", failureReason = "SIMULATED: unreachable" });
        await simulator.NotifyAsync(requestId: "after");

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(DeliveryState.Delivered, (await simulator.InboxMessageAsync("before")).Delivery);
        Assert.Equal(DeliveryState.Failed, (await simulator.InboxMessageAsync("after")).Delivery);
    }

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

    [Fact]
    public async Task Fail_without_a_reason_is_rejected()
    {
        using var simulator = new SimulatorFactory();
        var client = simulator.CreateClient();

        var forPatient = await client.PutAsJsonAsync($"/api/patient-behaviours/{TestData.PatientId}", new { delivery = "Fail", response = "None" });
        var forDefault = await client.PutAsJsonAsync("/api/patient-behaviours/default", new { delivery = "Fail", response = "None", failureReason = " " });

        Assert.Equal(HttpStatusCode.BadRequest, forPatient.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, forDefault.StatusCode);
        Assert.Empty(simulator.Store.GetBehaviours());
        Assert.Equal(DeliveryBehaviour.Deliver, simulator.Store.GetDefaultBehaviour().Delivery);
    }

    [Fact]
    public async Task Response_without_delivery_is_rejected()
    {
        using var simulator = new SimulatorFactory();
        var client = simulator.CreateClient();

        var silent = await client.PutAsJsonAsync($"/api/patient-behaviours/{TestData.PatientId}", new { delivery = "StaySilent", response = "Acknowledge" });
        var failed = await client.PutAsJsonAsync($"/api/patient-behaviours/{TestData.PatientId}", new { delivery = "Fail", response = "CallMe", failureReason = "SIMULATED: unreachable" });

        Assert.Equal(HttpStatusCode.BadRequest, silent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, failed.StatusCode);
        Assert.Empty(simulator.Store.GetBehaviours());
    }

    [Fact]
    public async Task Behaviour_for_a_non_synthetic_patient_identifier_is_rejected()
    {
        using var simulator = new SimulatorFactory();

        var response = await simulator.CreateClient().PutAsJsonAsync("/api/patient-behaviours/PT-0001", new { delivery = "Deliver", response = "None" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(simulator.Store.GetBehaviours());
    }

    [Fact]
    public async Task Every_delivery_outcome_is_recorded_as_a_reaction()
    {
        using var simulator = new SimulatorFactory();
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.Fail, PatientResponseBehaviour.None, "SIMULATED: unreachable"), "SYN-PT-0002");
        simulator.SetBehaviour(new PatientBehaviour(DeliveryBehaviour.StaySilent, PatientResponseBehaviour.None), "SYN-PT-0003");

        await simulator.NotifyAsync(requestId: "one", patientId: "SYN-PT-0001");
        await simulator.NotifyAsync(requestId: "two", patientId: "SYN-PT-0002");
        await simulator.NotifyAsync(requestId: "three", patientId: "SYN-PT-0003");

        var delivered = simulator.Reactions("SYN-PT-0001")[0];
        Assert.Equal(PatientReactionKind.Delivered, delivered.Reaction);
        Assert.True(delivered.Automatic);
        Assert.True(delivered.Succeeded);
        Assert.Equal(200, delivered.HttpStatusCode);
        Assert.Equal("Communication/one-delivery/_history/1", delivered.SourceEventId);

        Assert.Equal(PatientReactionKind.DeliveryFailed, Assert.Single(simulator.Reactions("SYN-PT-0002")).Reaction);

        var silent = Assert.Single(simulator.Reactions("SYN-PT-0003"));
        Assert.Equal(PatientReactionKind.NoDeliveryReport, silent.Reaction);
        Assert.Null(silent.SourceEventId);
        Assert.Null(silent.HttpStatusCode);
    }

    [Fact]
    public async Task Behaviour_routes_list_set_and_remove_behaviours()
    {
        using var simulator = new SimulatorFactory();
        var client = simulator.CreateClient();

        var put = await client.PutAsJsonAsync(
            "/api/patient-behaviours/SYN-PT-0009",
            new { delivery = "Deliver", response = "Acknowledge", respondAfter = "00:00:02", respondToNotification = 2, confidence = 4 });
        var assignment = await put.Content.ReadFromJsonAsync<PatientBehaviourAssignment>(SimulatorFactory.Json);
        var listed = await client.GetFromJsonAsync<List<PatientBehaviourAssignment>>("/api/patient-behaviours", SimulatorFactory.Json);
        var defaultPut = await client.PutAsJsonAsync("/api/patient-behaviours/default", new { delivery = "StaySilent", response = "None" });
        var defaultBehaviour = await client.GetFromJsonAsync<PatientBehaviour>("/api/patient-behaviours/default", SimulatorFactory.Json);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(2), assignment!.Behaviour.RespondAfter);
        Assert.Equal(2, assignment.Behaviour.RespondToNotification);
        Assert.Equal(4, assignment.Behaviour.Confidence);
        Assert.Null(assignment.ScenarioName);
        Assert.Equal("SYN-PT-0009", Assert.Single(listed!).PatientSyntheticId);
        Assert.Equal(HttpStatusCode.OK, defaultPut.StatusCode);
        Assert.Equal(DeliveryBehaviour.StaySilent, defaultBehaviour!.Delivery);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/patient-behaviours/SYN-PT-0009")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/patient-behaviours/SYN-PT-0009")).StatusCode);
    }

    [Theory]
    [InlineData("""{"delivery":"Deliver","response":"RateConfidence"}""")]
    [InlineData("""{"delivery":"Deliver","response":"Acknowledge","confidence":6}""")]
    [InlineData("""{"delivery":"Deliver","response":"None","confidence":3}""")]
    [InlineData("""{"delivery":"Deliver","response":"Acknowledge","respondToNotification":0}""")]
    [InlineData("""{"delivery":"Deliver","response":"Acknowledge","respondAfter":"-00:00:01"}""")]
    [InlineData("""{"delivery":"Sideways","response":"None"}""")]
    public async Task Behaviour_that_breaks_a_rule_is_rejected(string json)
    {
        using var simulator = new SimulatorFactory();

        var response = await simulator.CreateClient().PutAsync(
            $"/api/patient-behaviours/{TestData.PatientId}",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(simulator.Store.GetBehaviours());
    }
}
