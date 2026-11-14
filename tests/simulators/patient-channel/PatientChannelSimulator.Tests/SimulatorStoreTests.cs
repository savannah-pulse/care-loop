using PatientChannelSimulator.Outbound;
using PatientChannelSimulator.State;

namespace PatientChannelSimulator.Tests;

/// <summary>The in-memory store's own guarantees (WS8).</summary>
public class SimulatorStoreTests
{
    [Fact]
    public void A_request_received_twice_is_stored_once()
    {
        var store = new SimulatorStore();
        var first = TestData.Message();
        var replay = TestData.Message(receivedAt: TestData.Now.AddSeconds(30));

        Assert.True(store.TryAddInboxMessage(first, out var storedFirst));
        Assert.False(store.TryAddInboxMessage(replay, out var storedReplay));

        Assert.Same(first, storedFirst);
        Assert.Same(first, storedReplay);
        Assert.Single(store.GetInboxMessages());
        Assert.Same(first, store.FindInboxMessageByRequest(TestData.RequestId));
    }

    [Fact]
    public void Concurrent_deliveries_of_one_request_create_one_message()
    {
        var store = new SimulatorStore();
        var added = 0;

        Parallel.For(0, 64, _ =>
        {
            if (store.TryAddInboxMessage(TestData.Message(), out var _))
            {
                Interlocked.Increment(ref added);
            }
        });

        Assert.Equal(1, added);
        Assert.Single(store.GetInboxMessages());
    }

    [Fact]
    public void Concurrent_updates_of_one_message_are_not_lost()
    {
        var store = new SimulatorStore();
        store.AddInboxMessage(TestData.Message());

        Parallel.For(0, 200, _ =>
            store.UpdateInboxMessage(TestData.RequestId, m => m with { CallRequestCount = m.CallRequestCount + 1 }));

        Assert.Equal(200, store.FindInboxMessage(TestData.RequestId)!.CallRequestCount);
    }

    [Fact]
    public void Updating_an_unknown_message_throws()
    {
        var store = new SimulatorStore();

        Assert.Throws<KeyNotFoundException>(() => store.UpdateInboxMessage(TestData.Message()));
        Assert.Throws<KeyNotFoundException>(() => store.UpdateInboxMessage("missing", m => m));
    }

    [Fact]
    public void Log_lists_newest_first_and_drops_the_oldest_beyond_its_capacity()
    {
        var store = new SimulatorStore();
        for (var i = 0; i < SimulatorStore.LogCapacity + 10; i++)
        {
            store.AppendLogEntry(new FhirMessageLogEntry(
                $"entry-{i}", TestData.Now.AddSeconds(i), FhirMessageDirection.Inbound, "POST", "/fhir/CommunicationRequest", "CommunicationRequest", null, 201, null));
        }

        var entries = store.GetLogEntries();

        Assert.Equal(SimulatorStore.LogCapacity, entries.Count);
        Assert.Equal($"entry-{SimulatorStore.LogCapacity + 9}", entries[0].EntryId);
        Assert.Equal("entry-10", entries[^1].EntryId);
    }

    [Fact]
    public void A_caregiver_copy_does_not_count_as_a_notification_the_patient_received()
    {
        var store = new SimulatorStore();
        store.AddInboxMessage(TestData.Message());
        store.AddInboxMessage(TestData.Message(requestId: "caregiver-" + TestData.RequestId, recipientKind: RecipientKind.Caregiver));
        store.AddInboxMessage(TestData.Message(requestId: "other", patientId: "SYN-PT-0002"));

        Assert.Equal(1, store.CountNotifications(TestData.PatientId));
    }

    [Fact]
    public void Reactions_are_listed_oldest_first_for_the_requested_patients_only()
    {
        var store = new SimulatorStore();
        store.AppendReaction(Reaction("SYN-PT-0001", TestData.Now.AddSeconds(2)));
        store.AppendReaction(Reaction("SYN-PT-0002", TestData.Now.AddSeconds(1)));
        store.AppendReaction(Reaction("SYN-PT-0001", TestData.Now));

        var reactions = store.GetReactions(["SYN-PT-0001"]);

        Assert.Equal([TestData.Now, TestData.Now.AddSeconds(2)], reactions.Select(r => r.At));
    }

    [Fact]
    public void A_due_response_is_claimed_exactly_once()
    {
        var store = new SimulatorStore();
        store.SchedulePendingResponse(new PendingResponse("late", PatientResponseKind.Acknowledgement, null, TestData.Now.AddMinutes(10)));
        store.SchedulePendingResponse(new PendingResponse("due", PatientResponseKind.CallMe, null, TestData.Now.AddMinutes(1)));

        Assert.Empty(store.ClaimDueResponses(TestData.Now));
        var claimed = store.ClaimDueResponses(TestData.Now.AddMinutes(1));

        Assert.Equal("due", Assert.Single(claimed).MessageId);
        Assert.Empty(store.ClaimDueResponses(TestData.Now.AddMinutes(1)));
        Assert.Equal(1, store.CountPendingResponses());
    }

    [Fact]
    public void Runs_are_saved_and_replaced_by_identifier()
    {
        var store = new SimulatorStore();
        var run = new ScenarioRunState("run-1", "happy-path", ScenarioRunStatus.Running, TestData.Now, null, [TestData.PatientId], []);

        store.SaveRun(run);
        store.SaveRun(run with { Status = ScenarioRunStatus.Failed });

        Assert.Equal(ScenarioRunStatus.Failed, store.FindRun("run-1")!.Status);
        Assert.Null(store.FindRun("run-2"));
    }

    private static PatientReactionRecord Reaction(string patientId, DateTimeOffset at) =>
        new("message", patientId, PatientReactionKind.Delivered, true, null, at, 200, true, null);
}
