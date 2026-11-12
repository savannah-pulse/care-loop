using System.Net;
using System.Text.Json;

namespace CareLoop.Api.Tests;

public sealed class EventIntakeTests : IClassFixture<CareLoopApiFactory>
{
    private readonly CareLoopApiFactory factory;

    public EventIntakeTests(CareLoopApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Result_received_flows_through_stages_one_to_three_and_records_each_step()
    {
        using var client = factory.CreateClient();

        var (response, body) = await TestEvents.PostAsync(client, TestEvents.ResultReceived("evt-flow", "RES-FLOW"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("OWNER_ASSIGNED", body.GetProperty("stage").GetProperty("code").GetString());
        var caseId = body.GetProperty("caseId").GetGuid();

        var timeline = await TestEvents.GetAsync(client, $"/api/cases/{caseId}/timeline");
        Assert.Equal(
            ["ResultReceived", "CaseCreated", "OwnerAssigned"],
            timeline.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()!).ToArray());
        Assert.Equal(
            ["RESULT_RECEIVED", "CASE_CREATED", "OWNER_ASSIGNED"],
            timeline.EnumerateArray().Select(e => e.GetProperty("transition").GetProperty("to").GetProperty("code").GetString()!).ToArray());

        var summary = await TestEvents.GetAsync(client, $"/api/cases/{caseId}");
        Assert.Equal("queue", summary.GetProperty("owner").GetProperty("kind").GetString());
        Assert.Equal("synthetic-test-queue", summary.GetProperty("owner").GetProperty("id").GetString());
        Assert.Equal(0, summary.GetProperty("openExceptionCount").GetInt32());
    }

    [Fact]
    public async Task Replaying_a_source_event_is_idempotent()
    {
        using var client = factory.CreateClient();
        var envelope = TestEvents.ResultReceived("evt-replay", "RES-REPLAY");

        var (first, firstBody) = await TestEvents.PostAsync(client, envelope);
        var (second, secondBody) = await TestEvents.PostAsync(client, envelope);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(firstBody.GetProperty("eventId").GetGuid(), secondBody.GetProperty("eventId").GetGuid());
        Assert.Equal(firstBody.GetProperty("caseId").GetGuid(), secondBody.GetProperty("caseId").GetGuid());

        var timeline = await TestEvents.GetAsync(client, $"/api/cases/{firstBody.GetProperty("caseId").GetGuid()}/timeline");
        Assert.Equal(3, timeline.GetArrayLength());
    }

    [Fact]
    public async Task Concurrent_deliveries_of_one_event_create_exactly_one_case()
    {
        using var client = factory.CreateClient();
        var envelope = TestEvents.ResultReceived("evt-race", "RES-RACE");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => TestEvents.PostAsync(client, envelope)));

        Assert.All(results, r => Assert.True(
            r.Response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected status {r.Response.StatusCode}"));

        // A resend after the race settles converges on the single case.
        var (_, settled) = await TestEvents.PostAsync(client, envelope);
        var timeline = await TestEvents.GetAsync(client, $"/api/cases/{settled.GetProperty("caseId").GetGuid()}/timeline");
        Assert.Equal(3, timeline.GetArrayLength());
        var cases = await TestEvents.GetAsync(client, "/api/cases?pageSize=100");
        Assert.Equal(1, cases.GetProperty("items").EnumerateArray()
            .Count(c => c.GetProperty("resultRef").GetString() == "RES-RACE"));
    }

    [Fact]
    public async Task Reusing_a_source_event_id_with_different_content_is_held_for_review()
    {
        using var client = factory.CreateClient();
        var (_, original) = await TestEvents.PostAsync(client, TestEvents.ResultReceived("evt-conflict", "RES-CONFLICT"));

        var (response, body) = await TestEvents.PostAsync(
            client, TestEvents.ResultReceived("evt-conflict", "RES-CONFLICT", reviewedUrgency: "urgent"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("source_event_content_conflict", body.GetProperty("code").GetString());
        var caseId = original.GetProperty("caseId").GetGuid();
        var exceptions = await TestEvents.GetAsync(client, "/api/exceptions?pageSize=100");
        Assert.Contains(exceptions.GetProperty("items").EnumerateArray(), e =>
            e.GetProperty("caseId").GetGuid() == caseId && e.GetProperty("type").GetString() == "conflictingSourceEvent");
    }

    [Fact]
    public async Task A_new_event_for_a_result_that_already_has_a_case_is_held_not_applied()
    {
        using var client = factory.CreateClient();
        var (_, original) = await TestEvents.PostAsync(client, TestEvents.ResultReceived("evt-orig", "RES-DUP"));

        var (response, body) = await TestEvents.PostAsync(client, TestEvents.ResultReceived("evt-correction", "RES-DUP"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("result_already_has_case", body.GetProperty("code").GetString());
        Assert.Equal(original.GetProperty("caseId").GetGuid(), body.GetProperty("caseId").GetGuid());

        var timeline = await TestEvents.GetAsync(client, $"/api/cases/{original.GetProperty("caseId").GetGuid()}/timeline");
        Assert.Equal("heldForReview", timeline.EnumerateArray().Last().GetProperty("disposition").GetString());
        Assert.Equal("OWNER_ASSIGNED", (await TestEvents.GetAsync(client, $"/api/cases/{original.GetProperty("caseId").GetGuid()}"))
            .GetProperty("stage").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Missing_reviewed_urgency_raises_review_work_instead_of_guessing()
    {
        using var client = factory.CreateClient();

        var (response, body) = await TestEvents.PostAsync(
            client, TestEvents.ResultReceived("evt-no-urgency", "RES-NOURGENCY", reviewedUrgency: null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var summary = await TestEvents.GetAsync(client, $"/api/cases/{body.GetProperty("caseId").GetGuid()}");
        Assert.Equal(1, summary.GetProperty("openExceptionCount").GetInt32());
    }

    [Fact]
    public async Task Non_synthetic_events_are_rejected()
    {
        using var client = factory.CreateClient();
        var envelope = new
        {
            eventType = "ResultReceived", schemaVersion = "1.0", sourceSystem = "his-simulator",
            sourceEventId = "evt-real", occurredAt = "2026-01-05T08:00:00Z", synthetic = false,
            patientRef = "p", resultRef = "r",
        };

        var (response, body) = await TestEvents.PostAsync(client, envelope);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.GetProperty("errors").TryGetProperty("synthetic", out _));
    }

    [Fact]
    public async Task Unsupported_event_types_are_reported_not_dropped()
    {
        using var client = factory.CreateClient();
        var envelope = new
        {
            eventType = "PatientNotified", schemaVersion = "1.0", sourceSystem = "channel-simulator",
            sourceEventId = "evt-notify", occurredAt = "2026-01-05T08:00:00Z", synthetic = true,
        };

        var (response, body) = await TestEvents.PostAsync(client, envelope);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("unsupported_event_type", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Owner_assignment_is_enforced_for_humans_with_a_permitted_role()
    {
        using var client = factory.CreateClient();
        var (_, created) = await TestEvents.PostAsync(client, TestEvents.ResultReceived("evt-perm", "RES-PERM"));
        var caseId = created.GetProperty("caseId").GetGuid();

        var (response, body) = await TestEvents.PostAsync(
            client, TestEvents.OwnerAssigned(caseId, "assign-wrong-role", role: "clinician"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("actor_not_permitted", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Stages_endpoint_lists_the_ten_stages_in_order()
    {
        using var client = factory.CreateClient();

        var stages = await TestEvents.GetAsync(client, "/api/stages");

        Assert.Equal(10, stages.GetArrayLength());
        Assert.Equal("RESULT_RECEIVED", stages[0].GetProperty("code").GetString());
        Assert.Equal("CLOSED", stages[9].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Case_worklist_filters_by_stage_code()
    {
        using var client = factory.CreateClient();
        await TestEvents.PostAsync(client, TestEvents.ResultReceived("evt-list", "RES-LIST"));

        var owned = await TestEvents.GetAsync(client, "/api/cases?stage=OWNER_ASSIGNED&pageSize=100");
        var closed = await TestEvents.GetAsync(client, "/api/cases?stage=CLOSED");
        var bad = await client.GetAsync("/api/cases?stage=NOPE");

        Assert.Contains(owned.GetProperty("items").EnumerateArray(), c => c.GetProperty("resultRef").GetString() == "RES-LIST");
        Assert.Equal(0, closed.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Unknown_case_returns_not_found()
    {
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cases/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cases/{Guid.NewGuid()}/timeline")).StatusCode);
    }
}

public sealed class UnconfiguredOwnerRoutingTests : IClassFixture<UnconfiguredOwnerRoutingTests.NoQueueFactory>
{
    public sealed class NoQueueFactory() : CareLoopApiFactory(defaultQueueId: null);

    private readonly NoQueueFactory factory;

    public UnconfiguredOwnerRoutingTests(NoQueueFactory factory) => this.factory = factory;

    [Fact]
    public async Task Without_approved_routing_the_case_waits_at_stage_two_until_a_person_assigns_an_owner()
    {
        using var client = factory.CreateClient();

        var (response, created) = await TestEvents.PostAsync(client, TestEvents.ResultReceived("evt-noroute", "RES-NOROUTE"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("CASE_CREATED", created.GetProperty("stage").GetProperty("code").GetString());
        var caseId = created.GetProperty("caseId").GetGuid();
        var open = await TestEvents.GetAsync(client, "/api/exceptions?pageSize=100");
        Assert.Contains(open.GetProperty("items").EnumerateArray(), e =>
            e.GetProperty("caseId").GetGuid() == caseId && e.GetProperty("type").GetString() == "ownerUnassigned");

        var (assigned, assignedBody) = await TestEvents.PostAsync(client, TestEvents.OwnerAssigned(caseId, "assign-1"));

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        Assert.Equal("OWNER_ASSIGNED", assignedBody.GetProperty("stage").GetProperty("code").GetString());
        var summary = await TestEvents.GetAsync(client, $"/api/cases/{caseId}");
        Assert.Equal("person", summary.GetProperty("owner").GetProperty("kind").GetString());
        Assert.Equal(0, summary.GetProperty("openExceptionCount").GetInt32());

        var (again, againBody) = await TestEvents.PostAsync(client, TestEvents.OwnerAssigned(caseId, "assign-2", ownerId: "coordinator-2"));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("owner_transfer_not_supported", againBody.GetProperty("code").GetString());
    }
}
