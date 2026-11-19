# CareLoop API

The coordination engine: ASP.NET Core minimal APIs on .NET 10, EF Core on PostgreSQL, FHIR R4 through the Firely SDK.
It is not a clinical system. It is built as for production and is currently connected to the two simulators. Read the root `AGENTS.md` before changing anything. Paths below are relative to the repository root.

Domain, persistence/intake, FHIR, outbox/notifications, deadlines and the WS6 read APIs are implemented.
Remaining workstream stubs return **501**, including WS9 summary actions and WS10 simulation.
See `docs/workstreams.md` for ownership and status.

## Projects

| Project | What it is | References |
|---|---|---|
| `src/api/CareLoop.Domain` | Cases, tracks, rules, pathway definition, domain events | nothing |
| `src/api/CareLoop.Api` | Contracts, feature slices, infrastructure, background workers | Domain |
| `tests/api/CareLoop.Tests` | xUnit tests, fakes and synthetic FHIR samples | Domain, Api |

## Run

```bash
dotnet run --project src/api/CareLoop.Api
```

The API listens on `http://localhost:5080`. `GET /health` returns 200. The OpenAPI document is served at
`/openapi/v1.json` and written to `contracts/openapi/careloop.json` on every build.

Persistence and read endpoints require a database. Start one with `docker compose up db` from the repository root.

## Test

The root solution also contains the two simulators; to build and test only the API:

```bash
dotnet build src/api/CareLoop.Api && dotnet build tests/api/CareLoop.Tests
```

```bash
dotnet test tests/api/CareLoop.Tests
```

Or everything at once with `dotnet build CareLoop.sln` and `dotnet test CareLoop.sln`.

The build treats every warning as an error, including a missing XML documentation comment (`CS1591`).
Planned behaviours exist as named, skipped tests (`[Fact(Skip = "WSn: not implemented")]`). Un-skip a test when you
implement its behaviour; do not weaken or delete one to make it pass.

## Database migrations

The EF Core tool is pinned in `.config/dotnet-tools.json` at the repository root.

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> --project src/api/CareLoop.Api --output-dir Infrastructure/Persistence/Migrations
```

Migrations are applied at startup only when `Database:MigrateOnStartup` is `true` (Docker Compose sets it).

## Configuration

The base `appsettings.json` configures no external system and no simulator address. `appsettings.Development.json`,
used by `dotnet run`, adds the HIS simulator as the one external system, points the `Sms` and `Email` providers at the
Messaging simulator and gives each staff role a synthetic address; Docker Compose sets the same through environment variables. Production supplies its own entries.

| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:CareLoop` | local PostgreSQL | Database connection. Override with the environment variable `ConnectionStrings__CareLoop`. |
| `Database:MigrateOnStartup` | `false` | Apply migrations when the API starts. |
| `Pathways:Directory` | `Infrastructure/Pathways` | Folder the pathway definitions are loaded from. Development and Docker Compose use `Infrastructure/Pathways/demo`, which has the same rules with deadlines shortened for live demos. |
| `ExternalSystems:Systems:{systemId}:BaseUrl` | none | Address of one system of record (HIS, EMR, lab system). Any number may be configured. The key must equal the source system identifier that system sends; messages about a case go back to the system the case came from. |
| `Notifications:Sms:BaseUrl`, `Notifications:Email:BaseUrl` | none | Where each provider posts its messages, as JSON to `{BaseUrl}/messages`. In the prototype both are the messaging simulator. CareLoop reaches patients, caregivers and staff by text message and email only. |
| `Notifications:Sms:InboundSecret`, `Notifications:Email:InboundSecret` | none | Secret each channel's callbacks are signed with. Every message to `POST /notifications/inbound/sms` or `/email` must carry `X-CareLoop-Signature: sha256=<hex HMAC-SHA256 of the body>`; while the secret is unset, all are refused with 401. Set it from the environment (`SIMULATOR_INBOUND_SECRET` in Docker Compose), never in a committed file. |
| `Notifications:StaffContacts:{role}:Channel`, `…:Address` | none | How each staff role is notified: `Sms` or `Email`, and its address. An optional `default` entry covers roles with none. A role with neither cannot be reached, and its notices are retried. |
| `Notifications:TimeZone` | `Asia/Dubai` (UTC when empty) | The IANA time zone messages give dates and times in: appointment times for patients and caregivers, and when the previous escalation level was acknowledged. An unknown zone stops start-up. |
| `Notifications:FacilityNames:{facilityId}` | none | The name a staff message gives a facility ("A case at Family Health Centre needs your action"). A facility with no entry is named by its identifier in words. |
| `Notifications:EnabledProviders` | `[ "Sms", "Email" ]` | Enabled notification providers. Only `Sms` and `Email` are accepted; any other name stops the host at start-up. |
| `Notifications:PerEventOverrides` | `{ }` | Optional provider list per domain event name. |
| `Outbox:Enabled` | `false` (on in `appsettings.json`) | Run the outbox dispatcher. Test hosts leave it off. |
| `Outbox:PollInterval` | `00:00:02` | How often the dispatcher looks for due deliveries. |
| `Outbox:MaxAttempts` | `10` | Attempts per subscriber before a delivery is parked for a person to look at. |
| `Outbox:LeaseDuration` | `00:05:00` | How long one instance's claim on a delivery lasts. Several API instances may share the database; a claim left by a stopped instance lapses after this and the delivery is picked up again. |
| `DeadlineWorker:Enabled` | `true` (in `appsettings.json`) | Run the deadline worker. Test hosts can disable the background loop and call `SweepAsync` explicitly. |
| `DeadlineWorker:Interval` | `00:00:30` | Technical sweep interval, positive and within `Task.Delay`'s supported range; not a clinical deadline. Development settings and Docker Compose use `00:00:01` because the demo pathway's deadlines last seconds. |
| `Ai:Provider` | `NoOp` | `NoOp` (AI off) or `AzureOpenAi`. Azure settings come from environment variables only. |

Clinical deadlines are **not** configuration keys here. They live in
`src/api/CareLoop.Api/Infrastructure/Pathways/breast-screening.json`, where every value is a placeholder.

The API always runs on real time. For a live demo it reads `Pathways/demo/breast-screening.json` instead: the same
definition with every duration divided by 1800, so a one-day deadline passes in 48 seconds. `DemoPathwayFileTests`
fails if the two files differ in anything but that factor. Never point a real deployment at the demo folder.

### Deadlines and escalation (WS5)

The worker checks open cases with a due time using `IClock`. At the start of the configured warning window,
it writes one `DeadlineApproaching` event to the outbox per case, next action and due time. The warning's source
event identity is persisted, so repeated sweeps and worker restarts do not repeat it. A deadline is overdue
strictly after its due instant; a notification still awaiting acknowledgement also gets the `Unacknowledged` flag.

Each due escalation step is raised through `Case.Escalate`, in ladder order, with oversight addressed to
`{role}:{facility}`. A delayed sweep catches up every due step. Acknowledging a step does not stop escalation;
the next notice includes the previous level's acknowledgement. Resolved escalations and those raised before
the current due time do not block a new deadline's ladder.

Warnings, new exception flags and escalations are saved as internal `DeadlineElapsed` source events through
`ICaseRepository.SaveAsync`, atomically with audit and outbox records. The existing dispatcher publishes them;
the worker sends nothing directly and changes neither owner, urgency, stage nor deadline. Case checks use fresh
scopes, log failures without patient detail and retry on the next sweep. Optimistic concurrency rolls back a stale
check rather than overwriting an intervening event.

Run the focused tests:

```bash
dotnet test tests/api/CareLoop.Tests --filter "FullyQualifiedName~DeadlinePolicyTests|FullyQualifiedName~DeadlineWorkerTests"
```

## Routes

| Method | Route | Owner |
|---|---|---|
| GET | `/health` | works |
| POST | `/fhir/{resourceType}` | WS3 |
| PUT | `/fhir/{resourceType}/{id}` | WS3 |
| GET | `/fhir/Task/{id}` | WS3 |
| GET | `/fhir/metadata` | WS3 |
| GET | `/cases` | WS6 |
| GET | `/cases/{caseId}` | WS6 |
| GET | `/cases/{caseId}/timeline` | WS6 |
| POST | `/cases/{caseId}/escalations/{escalationId}/acknowledgement` | WS6 |
| POST | `/cases/{caseId}/call-outcome` | WS6 |
| GET | `/exceptions` | WS6 |
| GET | `/metrics` | WS6 |
| GET | `/programmes` | WS6 |
| GET | `/programmes/{pathwayId}` | WS6 |
| POST | `/metrics/simulation` | WS10 |
| POST | `/cases/{caseId}/handoff-summary` | WS9 |
| POST | `/cases/{caseId}/handoff-summary/{summaryId}/approval` | WS9 |
| POST | `/cases/{caseId}/handoff-summary/{summaryId}/rejection` | WS9 |

External systems use `/fhir/...` only. The plain REST routes are for the CareLoop dashboard. Delivery reports and
replies to text messages and emails arrive at `POST /notifications/inbound/sms` and `/notifications/inbound/email`,
signed by the service that sent them.

### Read access and acknowledgement (WS6)

REST reads require `X-Demo-User` and a human `X-Demo-Role`; non-coordinators also require `X-Demo-Facility`.
These caller-stated headers are prototype identity, not production authentication. Missing identity is 401,
unsupported roles are 403 and malformed headers/filters are 400.

Staff reads default to their facility (the current team boundary). An explicit different `facilityId`, or a case
outside that facility, requires a nonblank `reason` of at most 1000 characters. Coordinators have global aggregate
metrics, but their case reads are limited to cases with an open escalation addressed to the coordinator.
Names, MRNs, patient references and the hospital's result identifier are null on coordinator case, detail and
exception responses: the coordinator sees CareLoop's case number only. The outcome evidence reference on the case
(including the acknowledgement's answer) and every timeline entry's `evidenceReference`, which name hospital records,
are null for the coordinator too. Where a timeline entry's `reason` or `actorReference` quotes one of the case's
identifiers, as a refused resend of a result does, the coordinator reads `(hidden)` in its place. Other roles read all
of these unchanged.

Each timeline entry has an `origin`: `hospital` (the case's source system), `careloop` (CareLoop itself, including a
call a nurse recorded and CareLoop's record of what it sent), `sms` or `email` (a delivery report or the patient's reply
on that channel). Each applied patient-contact entry also says in plain words what it recorded.

Recording a call (`POST /cases/{caseId}/call-outcome`) answers the patient's request for one: the case moves on to
arranging the follow-up, or, after a rating alone, back to waiting for the acknowledgement. A text reply of `1` never
cancels a call the patient asked for. The access is written to the read audit with its reason, like a case read.
Each authorized-identity read records one `CaseReadRecord`; denied facility/role reads use a denied view name.
Read audits do not change case state and are not timeline events.

`GET /cases` supports `stage`, `ownerReference`, `escalated`, `facilityId`, `patientReference`, `skip` and `take`.
Patient searches are restricted to `clinician` and `receiving-clinician`. Paging defaults to 50, is capped at 200,
and uses newest-updated order with a case-ID tie-break. `GET /exceptions` returns open flagged cases in deadline
order, pageable with `skip` and `take` (default and maximum 200); a row with no deadline or next action carries
nulls instead of failing the list. Timelines include applied and rejected events in received-time/projection-version order, joined to their
source event for source attribution. Display labels come from the Domain rule, never the dashboard.
Programmes describe current definitions; historic pathway-version retention is not implied.

Acknowledgement checks the addressed role and current escalation ID, maps refusals to 403/409 and records both
applied and Domain-refused attempts through `ICaseRepository.SaveAsync`. It changes no stage, urgency, owner,
exception or deadline. A stale concurrent write returns 409 instead of overwriting the case.

### Coordination measures (WS6)

Measures use applied audit snapshots only, including source-received progress rather than inferred skipped labels:

- `Measures` supplies numerator, denominator and nullable rate for acknowledgement, closure, understanding,
  next-step timeliness, accepted referral plus attended appointment, attended appointments, timely treatment start
  and signed closure evidence.
- `TimeToStep` supplies sample count and nullable median elapsed time from the screening result (or case creation
  when no result source timestamp exists) to acknowledgement, consultation, appointment, care started and closure.
  Care started means a `treatment-started` or `surveillance-started` outcome; all time is clock time.
- Next-step timeliness uses the most recent deadline episode whose due instant has passed. Repeated snapshots do
  not restart an episode. Completion at the due instant is on time.
- Treatment timeliness uses cases with `treatment-plan` as denominator and subsequent `treatment-started` at or
  before that plan's captured `start-treatment` deadline as numerator. A referral is not completed care.
- Current and historically reached display-label counts, closure reasons and open exception counts remain distinct.
- A display label counts as reached once the event that puts a case there was applied, even when the case moved on at
  once: the screening result receives the result, opens the case and assigns an owner in one event, and an outcome
  recorded after every required track is complete goes straight to closure review. So every case counts for result
  received and case opened, and every case past follow-up counts for outcome recorded. The optional steps (patient
  acknowledged, consultation, follow-up, and closure review, which a closure by documented exception bypasses) count
  only for cases where they happened, even when a later step hid them from the label.
- A confidence rating is recorded by a nurse navigator after a call (`POST /cases/{caseId}/call-outcome`), never by a
  text or email reply. `ConfidenceRate` is rating participation among notified cases, not a clinical outcome. `AverageConfidence`
  separately averages each case's latest 1-5 rating.

Empty populations have null rates/medians, not zero success. The API does not add simulation labels, a working-hours
calendar, AI narrative or the WS10 escalation-off comparison.

Focused validation (126 passed, none skipped):

```sh
dotnet test tests/api/CareLoop.Tests/CareLoop.Tests.csproj --no-restore --filter "FullyQualifiedName~ReadEndpointsTests|FullyQualifiedName~CaseDisplayLabelTests|FullyQualifiedName~EscalationAcknowledgementTests|FullyQualifiedName~DeadlineWorkerTests|FullyQualifiedName~DeadlinePolicyTests" --verbosity minimal
```
