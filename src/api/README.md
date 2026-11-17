# CareLoop API

The coordination engine: ASP.NET Core minimal APIs on .NET 10, EF Core on PostgreSQL, FHIR R4 through the Firely SDK.
It is not a clinical system. It is built as for production and is currently connected to the two simulators. Read the root `AGENTS.md` before changing anything. Paths below are relative to the repository root.

Domain, persistence/intake, FHIR, outbox/notifications, deadlines and the WS6 read APIs are implemented.
Remaining workstream stubs return **501**, including WS9 summary actions and WS10 simulation.
The three AI agent endpoints also answer **501** until `Ai:Agents:Enabled` is `true` (see
[AI agents on the dashboard](#ai-agents-on-the-dashboard)).
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
| `Ai:Agents:Enabled` | `false` | Switch on the three Foundry agents the dashboard asks for words. While `false` their endpoints answer 501 and nothing is sent. When `true`, the settings below are checked at start-up and a bad value stops the host. |
| `Ai:Agents:ProjectEndpoint` | none | The Foundry project, `https://{resource}.services.ai.azure.com/api/projects/{project}`. `https` only. Set it from the environment (`Ai__Agents__ProjectEndpoint`), never in a committed file. |
| `Ai:Agents:RoleBriefing`, `…:ImpactSummary`, `…:PatientNotification` | `role-briefing`, `impact-summary`, `patient-notification` | The agents' names in the project. |
| `Ai:Agents:TimeoutSeconds` | `30` | How long one agent may take, token included, before the endpoint answers 503. From 1 to 300. |
| `Ai:Agents:AccessToken` | none | A ready Entra token for `https://ai.azure.com/.default`, used instead of the API's identity. Honoured only in the `Development` environment (ignored, with a warning, anywhere else). For a local test only: set it in the shell for one session and never commit it. |
| `Ai:Agents:UrgentCodes` | `[ "PLACEHOLDER-URGENCY-B" ]` | Which clinician-reviewed urgency codes the role briefing counts and orders as urgent. Must match the dashboard's table of urgency codes; a placeholder like the pathway's codes. |

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
| POST | `/ai/role-briefing` | AI agents (`Ai:Agents`) |
| POST | `/ai/impact-summary` | AI agents (`Ai:Agents`) |
| POST | `/cases/{caseId}/patient-message-draft` | AI agents (`Ai:Agents`) |

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
exception responses: the coordinator sees CareLoop's case number only.

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
- A confidence rating is recorded by a nurse navigator after a call (`POST /cases/{caseId}/call-outcome`), never by a
  text or email reply. `ConfidenceRate` is rating participation among notified cases, not a clinical outcome. `AverageConfidence`
  separately averages each case's latest 1-5 rating.

Empty populations have null rates/medians, not zero success. The API does not add simulation labels, a working-hours
calendar or the WS10 escalation-off comparison, and `GET /metrics` carries no AI narrative: `POST /ai/impact-summary`
writes one separately, from the same measures.

Focused validation (98 passed, none skipped):

```sh
dotnet test tests/api/CareLoop.Tests/CareLoop.Tests.csproj --no-restore --filter "FullyQualifiedName~ReadEndpointsTests|FullyQualifiedName~CaseDisplayLabelTests|FullyQualifiedName~EscalationAcknowledgementTests|FullyQualifiedName~DeadlineWorkerTests|FullyQualifiedName~DeadlinePolicyTests" --verbosity minimal
```

### AI agents on the dashboard

Three Microsoft Foundry agents write a few words for staff. CareLoop works out every number, deadline and order; the
agents only put them into words, and what they write is a draft: it is not stored, sets no urgency, changes no case and
sends no message. `docs/ai-design/integration.md` has the full boundary.

| Route | Agent (`Ai:Agents:…`) | Who | Built from |
|---|---|---|---|
| `POST /ai/role-briefing` (optional `facilityId`, `reason`) | `RoleBriefing` | every staff role | The reader's role and facility, counts of the open cases in the worklist scope (open, overdue, escalated, urgent, due today, needs an owner) and up to five of them, most pressing first, each by case number, programme, step, next action, due status and flags |
| `POST /ai/impact-summary` (optional `facilityId`, `reason`) | `ImpactSummary` | every staff role | The aggregate measures `GET /metrics` returns for the same reader, nothing per case |
| `POST /cases/{caseId}/patient-message-draft` (optional `channel` = `Sms` or `Email`, `reason`) | `PatientNotification` | nurse navigator, clinician | The next action, the upcoming appointment's day, time and place in local time, the channel and the reply codes; `{first name}` stands for the name |

Each answers `AiTextResponse { agent, text, generatedAt }`. Identity, facility scope and reasons follow the reads they
mirror (the worklist, `GET /metrics`, a case read), each call is recorded in `CaseReads` (views `role-briefing`,
`impact-summary`, `patient-message-draft`, with `-denied` for a refusal), and the coordinator's briefing covers only the
cases escalated to the coordinator. While `Ai:Agents:Enabled` is `false` they answer 501 with the not-implemented body.
When the agent fails (no token, 401, 403, 429, 5xx, a timeout, an incomplete or empty answer) they answer 503 with the
title "AI could not write this" and no text. No prompt carries a patient's name, contact details, MRN, patient
reference, the hospital's result identifier, a staff reference or free text.

`FoundryAgentClient` posts `{"input":[{"role":"user","content":"<prompt>"}]}` to
`{ProjectEndpoint}/agents/{agent}/endpoint/protocols/openai/responses?api-version=v1` with an Entra bearer token for
`https://ai.azure.com/.default`, and reads `output_text`, or every `output[].content[].text`. It logs the agent, status
and duration, and the agent's error code on a failure, never the prompt or the text.

Tokens come from `DefaultAzureCredential`: the managed identity when hosted in Azure (`AZURE_CLIENT_ID` picks a
user-assigned one), the developer's Azure CLI or Visual Studio sign-in locally. That identity needs the **Azure AI
User** role on the Foundry project, or every call answers 403 and the endpoints answer 503. To test locally with a
token from your own sign-in instead:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Ai__Agents__Enabled = "true"
$env:Ai__Agents__ProjectEndpoint = "https://<resource>.services.ai.azure.com/api/projects/<project>"
$env:Ai__Agents__AccessToken = az account get-access-token --scope https://ai.azure.com/.default --query accessToken -o tsv
dotnet run --project src/api/CareLoop.Api
```

On Windows also set `$env:Notifications__TimeZone = "Arabian Standard Time"`: with invariant globalization the host
cannot resolve the IANA name `Asia/Dubai`.

```sh
dotnet test tests/api/CareLoop.Tests --filter "FullyQualifiedName~CareLoop.Tests.Api.Agents"
```
