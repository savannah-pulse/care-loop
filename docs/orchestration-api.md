# Orchestration API

The orchestration API is the single authority for case state: it validates, deduplicates, authorizes, applies guarded stage transitions, and records audit history. This page covers how the .NET code is organized and how several engineers can extend it in parallel. Product rules live in [architecture.md](architecture.md) and the event envelope in [../contracts/README.md](../contracts/README.md).

## Layout

```text
src/api/
  CareLoop.Domain/          No dependencies. Stages, case aggregate, transition guard, event log entities.
  CareLoop.Application/     Intake pipeline, event handlers, automatic stage steps, read-model contracts.
  CareLoop.Infrastructure/  EF Core mappings, repositories, queries. The only project that knows the database.
  CareLoop.Api/             Thin host: endpoint modules, JSON/problem-details setup, configuration.
tests/api/CareLoop.Api.Tests/
```

Dependencies point inward (Api -> Infrastructure -> Application -> Domain). Handlers depend on ports (`ICaseRepository`, `ICaseEventStore`, `IUnitOfWork`), never on EF Core.

## Ten-stage flow

[`StageCatalog`](../src/api/CareLoop.Domain/Stages/StageCatalog.cs) is the single definition of the ten stages. [`StageTransitionGuard`](../src/api/CareLoop.Domain/Cases/StageTransitionGuard.cs) is the only place that decides whether a stage change is legal: exactly one stage forward, never backwards or skipping, and `CLOSED` only by a human actor with a typed reason and evidence references or an authorized exception. No handler can bypass it because `CoordinationCase.TryAdvance` calls it.

Each event goes through `EventIntakeService`:

1. Validate the envelope (synthetic-only, supported schema version, required fields, actor).
2. Route to the `ICaseEventHandler` for the event type (`422` if none).
3. Authorize the actor against the handler's `ActorPermission` (`403`).
4. Idempotency check on `(sourceSystem, sourceEventId)`. Same content returns the original result (`200`). Same ID with different content, or a second event for a result that already has a case, is recorded or flagged as a `ConflictingSourceEvent` exception and returns `409`; it never changes the case.
5. Run the handler, then run `IAutomaticStageStep`s for the next stages until one declines.
6. Commit once. Event log, stage transitions, exceptions, and case projection are saved atomically. A unique index on the idempotency key and a version token on the case make concurrent delivery safe: the loser gets `409 concurrent_update` and can resend.

| # | Stage | Driver (per architecture diagram) | Entered by | Status |
|---|---|---|---|---|
| 1 | `RESULT_RECEIVED` | HIS | `ResultReceived` from a source system ([handler](../src/api/CareLoop.Application/Stages/S01_ResultReceived/ResultReceivedHandler.cs)) | Implemented |
| 2 | `CASE_CREATED` | API | Automatic step after stage 1 | Implemented |
| 3 | `OWNER_ASSIGNED` | API | Automatic queue assignment, or `OwnerAssigned` from a coordinator or facility lead when no routing is configured | Implemented |
| 4 | `PATIENT_NOTIFIED` | Patient channel | `PatientNotified` (delivery) | Not started |
| 5 | `PATIENT_ACKNOWLEDGED` | Patient channel | `PatientAcknowledged` (acknowledgement, teach-back) | Not started |
| 6 | `CONSULTATION_PENDING` | HIS | `ConsultationRequested` | Not started |
| 7 | `FOLLOW_UP_IN_PROGRESS` | HIS | `FollowUpStarted` (referral and appointment) | Not started |
| 8 | `CARE_OUTCOME_DOCUMENTED` | HIS | `OutcomeDocumented` | Not started |
| 9 | `CLOSURE_REVIEW` | Clinician | `ClosureReviewRequested` | Not started |
| 10 | `CLOSED` | Clinician | `ClosureAuthorised` (authorized human, evidence or documented exception) | Not started |

The driver is metadata exposed by `GET /api/stages` and used to split ownership; it is not yet enforced. Restricting an event to a specific source system (HIS versus patient channel) needs the source registry in the event contract. `EscalationRaised` is a supporting event from the deadline worker (escalation chain owner, facility lead, coordinator); it never changes the stage.

Where configuration or evidence is missing, the case stays put and visible review work is raised instead of inventing a value: no configured owner queue raises `OwnerUnassigned` and the case waits at stage 2; a result without a reviewed urgency raises `ReviewedUrgencyMissing`.

## Adding work for a stage

Nothing central needs editing. Handlers, steps, and endpoint modules are discovered by assembly scanning, and EF mappings by `ApplyConfigurationsFromAssembly`.

1. Agree the event contract first (see the contract-first checklist in [development.md](development.md)).
2. Add `Application/Stages/Sxx_<Stage>/<Event>Handler.cs` implementing `ICaseEventHandler`. Declare who may submit it via `Permission`, append the primary event with `context.AppendPrimaryEvent`, change state only through the case aggregate and `context.Advance`, and return `HandlerResult.Accepted/Rejected/HeldForReview`. Handlers that can leave a stage without outside input implement `IAutomaticStageStep` instead.
3. Add an endpoint module or read model only if the stage needs one; `POST /api/events` already accepts every registered event type.
4. Add tests next to `EventIntakeTests`. `StageModuleRegistrationTests` fails if two handlers claim one event type or two steps target one stage.

Keep one engineer per stage folder to avoid merge conflicts. Shared files (`StageTransitionGuard`, `CoordinationCase`, `CaseEventContext`, the envelope) change by pull request with review from the other stage owners.

## Endpoints

| Method and path | Purpose |
|---|---|
| `POST /api/events` | Submit an event. `201` accepted, `200` replay, `400` invalid, `403` actor not permitted, `404` unknown case, `409` rejected/held/concurrent, `422` unsupported event type. |
| `GET /api/stages` | The ten stages, their entry events, and the component that drives each. |
| `GET /api/cases` | Paged worklist. Filters: `stage` (code), `hasOpenExceptions`, `ownerKind`, `ownerId`, `page`, `pageSize` (max 100). |
| `GET /api/cases/{id}` and `/timeline` | Case summary; ordered event timeline with the stage transition each event caused. |
| `GET /api/exceptions` | Paged exception queue. Open by default; `status=resolved` or `all=true` for others. |

OpenAPI is served at `/openapi/v1.json` in Development. Sample calls are in `src/api/CareLoop.Api/CareLoop.Api.http`.

## Known gaps and open decisions

- **Database:** the architecture diagram names PostgreSQL or Azure SQL for the event log and coordination store; that choice is still open. SQLite is a local placeholder. When the team chooses, change the `UseSqlite` call and `UnitOfWork` error codes, add migrations, and drop the SQLite `DateTimeOffset` convention in `CareLoopDbContext`. The schema is created with `EnsureCreated` only when `CareLoop:Database:EnsureCreated` is true.
- **Identity and roles:** the actor and role come from the envelope and are checked by `ActorPermission`, but they are not authenticated. Role names in `ActorRoles` are demo placeholders. Replace with real authentication before any non-local use; read endpoints are also unauthenticated.
- **Owner routing:** `CareLoop:OwnerAssignment:DefaultQueueId` is unset by default. Development and the compose file set a clearly synthetic placeholder.
- **Not yet built:** stages 4-10, owner transfer (offer and acceptance), exception resolution, deadline escalation, and an outbox for notifying the Python workflow. Rejected events are logged but not stored.
- **Scale notes:** the worklist uses offset paging and the exception count is a correlated subquery; move to keyset paging and a projected count once volumes justify it.
