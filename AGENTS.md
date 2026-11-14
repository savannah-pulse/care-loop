# CareLoop

Guidance for anyone working in this repository: people and AI coding agents alike, whichever tool or model they use. Read this file fully before writing code.

## What this is

CareLoop is a coordination and accountability layer for screening follow-up. Every actionable screening result becomes a **case** that always has a stage, an owner, a deadline and, at the end, closure evidence. The prototype targets one Abu Dhabi breast-screening follow-up pathway and is being built for a hackathon ("Closing the Screening-to-Care Loop").

The goal is a production system that handles real patient data. The current build is a prototype that runs on simulators and synthetic data. Treat that as the current stage, not as a design limit: do not write code, contracts or documentation that assume data is always synthetic.

CareLoop is **not** a clinical system. Hospital systems (HIS/EMR) remain the systems of record. CareLoop correlates events, tracks ownership and deadlines, raises escalations and blocks unsafe closure.

## Non-negotiable constraints

1. **Real patient data never enters the repository.** Source, tests, samples, scenarios, simulators, fixtures and local or demo environments use synthetic data only. Never commit real patient data, credentials or live endpoint addresses. The API and the dashboard are built as for production: they do not know or care whether the systems they talk to are simulators, and carry no simulated-data flags, labels or branches. Labelling simulated records and messages as simulated is the simulators' job.
2. **No clinical decisions.** CareLoop never diagnoses, never sets or changes urgency, and never creates a clinical outcome. Urgency always arrives clinician-reviewed from the HIS.
3. **Closure needs a human.** A case closes only on an authorized human closure event carrying pathway-specific evidence or a documented exception. The API must reject anything else and audit the attempt.
4. **AI output is a draft.** AI can draft a handoff summary from facts a person has finalized. A person approves or rejects it, and approval is an event. AI cannot set urgency, alter source data, close a case or send a message, because it is given no way to. Under the DoH standard (§3.8.1) the reporting physician informs the patient, so AI never discloses a result. [`docs/api-ai-workflow-handoff.md`](docs/api-ai-workflow-handoff.md) is the full boundary: what AI may do, what it is sent, and what happens when it fails.
5. **Deadlines are configuration.** Never hard-code a clinical deadline. All deadlines come from the pathway definition and are placeholders until a clinical adviser approves them. CareLoop always runs on real time and never speeds up its clock: a live demo uses the demo pathway folder, whose deadlines are shortened, selected through `Pathways:Directory`.

If a task appears to conflict with any of these, stop and ask.

## Not in scope yet

These are deferred, not forbidden. Build the seams for them now and leave the implementations for later.

- **Real SMS and email delivery.** For now the simulator notification provider is the only one enabled. SMS and email providers come later, behind the same `INotificationProvider` interface, and are switched on through configuration.
- **Arabic templates and right-to-left layout.** English only for now; keep everything language-keyed. A template missing in the recipient's language falls back to English, so a patient is never left unnotified; a malformed language tag fails.
- **Live HIS, Malaffi or patient-channel integration.** The simulators stand in for these.
- **AI beyond the handoff summary.** Patient-facing wording comes from approved templates, not a model; AI translation and the periodic stage-insight narrative are designed but not built. [`docs/ai-spec.md`](docs/ai-spec.md) proposes all three as extensions; they are deferred here, and the handoff summary is the only AI capability in scope.

## Before real patient data

The prototype is not yet fit for real patient data. These are prerequisites for a production deployment, not limits on the design. Build so that each can be met without changing the Domain or the contracts.

- **Authentication and authorization.** `DemoRoles` (identity stated in request headers) is replaced by real sign-in for dashboard users and authenticated system-to-system access on `/fhir`.
- **Clinical approval.** The pathway definition and every message template are approved by a clinical adviser; no placeholder remains.
- **Live integrations.** HIS, patient channel, SMS and email are implemented behind the existing seams and switched on through configuration.
- **Security and data protection.** Encryption in transit and at rest, managed secrets, access logging, retention rules and a review against the health-data regulation that applies to the deployment. The case store holds patient names and identifiers, so it is personal health data from the first real record.

## Repository layout

```
care-loop/
├── README.md
├── AGENTS.md                           the rules for this repository (this file)
├── CareLoop.sln                        every .NET project (API, Domain, simulators and their tests)
├── Directory.Build.props               shared .NET build rules (XML docs enforced, warnings as errors)
├── global.json                         .NET SDK pin
├── docker-compose.yml                  database, api, web, AI workflow, both simulators
├── .config/dotnet-tools.json           dotnet-ef
│
├── contracts/
│   └── openapi/careloop.json           the API's OpenAPI document, rewritten on every API build
│
├── docs/
│   ├── workstreams.md                  who owns which files (WS1 to WS10)
│   ├── architecture.md, development.md, releasing.md, component-register.md
│   ├── api-ai-workflow-handoff.md      what AI may do, and how the API and the Python service share the work
│   ├── ai-spec.md                      the AI design proposal; the handoff doc above is the contract
│   └── ui-design/                      screen designs
│
├── src/
│   ├── api/
│   │   ├── README.md
│   │   ├── CareLoop.Domain/            class library, references nothing
│   │   │   ├── Cases/                  Case, CaseStage, Assignment, Escalation, ClosureDecision, ICaseRepository
│   │   │   ├── Tracks/                 NotificationTrack, NotificationAttempt, ConsultationTrack, ReferralTrack, AppointmentTrack
│   │   │   ├── Recipients/             Recipient, ContactPoint, NotificationChannel
│   │   │   ├── Events/                 SourceEvent, CaseEventType, AuditEvent
│   │   │   │   └── DomainEvents/       IDomainEvent, NotificationRequested, HandoffProposed, HandoffRejected,
│   │   │   │                           ReferralRejected, CallRequested, DeadlineApproaching, EscalationRaised, CaseClosed,
│   │   │   │                           HandoffSummaryApproved
│   │   │   ├── Rules/                  TransitionRules, OwnershipRules, ClosureGate, DeadlinePolicy
│   │   │   ├── Pathways/               PathwayDefinition, IPathwayProvider
│   │   │   └── Time/                   IClock
│   │   │
│   │   └── CareLoop.Api/               ASP.NET Core, references Domain
│   │       ├── Program.cs
│   │       ├── Dockerfile              build context is the repository root
│   │       ├── appsettings.json
│   │       ├── Contracts/              namespace CareLoop.Contracts
│   │       │   ├── Events/             EventEnvelope, Payloads/ (eight payload records) - internal only
│   │       │   ├── Cases/              CaseSummaryResponse, CaseDetailResponse, TimelineEntryResponse
│   │       │   ├── Exceptions/         ExceptionItemResponse
│   │       │   ├── Metrics/            MetricsResponse, SimulationResponse
│   │       │   └── Summaries/          HandoffSummaryResponse
│   │       ├── Features/
│   │       │   ├── Events/             EventIngestor, IEventHandler, Handlers/ (one per event type)
│   │       │   ├── Fhir/               FhirEndpoints, GetCapabilityStatement, IFhirInboundMapper, StatusMap
│   │       │   │   ├── Inbound/        one mapper per inbound resource
│   │       │   │   └── Outbound/       FhirPublisher (outbox subscriber) and outbound mappers
│   │       │   ├── Outbox/             IDomainEventSubscriber, OutboxDispatcher
│   │       │   ├── Notifications/      NotificationRouter, RecipientResolver, NotificationSender,
│   │       │   │                       NotificationOptions, TemplateRenderer, INotificationProvider,
│   │       │   │                       IInboundNotificationHandler, ReceiveInboundNotification
│   │       │   ├── Cases/              ListCases, GetCase, GetTimeline
│   │       │   ├── Exceptions/         GetExceptions
│   │       │   ├── Metrics/            GetMetrics, RunSimulation
│   │       │   └── Summaries/          IAiProvider, fact and draft contracts, DraftHandoffSummary,
│   │       │                           ApproveSummary, RejectSummary, AutoDraftOnHandoff
│   │       ├── Infrastructure/
│   │       │   ├── Persistence/        CareLoopDbContext, EfCaseRepository, Outbox/, Configurations/, Migrations/
│   │       │   ├── Fhir/               FhirHttpClient
│   │       │   ├── Notifications/      SimulatorNotificationProvider, SimulatorInboundNotificationHandler,
│   │       │   │                       SmsNotificationProvider, EmailNotificationProvider
│   │       │   ├── Ai/                 NoOpAiProvider (default), AiWorkflowProvider, FoundryDirectProvider
│   │       │   ├── Pathways/           JsonPathwayProvider, PathwayOptions, breast-screening.json,
│   │       │   │                       demo/breast-screening.json (same rules, shortened deadlines)
│   │       │   ├── Templates/en/       approved message templates (standard and SMS-safe)
│   │       │   ├── Time/               SystemClock
│   │       │   └── Auth/               DemoRoles
│   │       └── Worker/                 DeadlineWorker
│   │
│   ├── web/                            CareLoop dashboard only (React, TypeScript, Vite); a starter today
│   │
│   ├── ai_workflow/                    Python AI workflow service (see its README); proposes, never decides.
│   │                                   Owned by WS9; it owns the handoff summary prompt and the Foundry call
│   │
│   ├── worker/                         README only: the deadline worker lives in CareLoop.Api/Worker
│   │
│   └── simulators/
│       ├── scenarios/                  happy-path, silent-patient, rejected-referral, broken-handoff, cohort (JSON)
│       ├── his/
│       │   ├── README.md, Dockerfile
│       │   └── HisSimulator/           ASP.NET minimal API: Outbound/, Inbound/, Scenarios/, State/, Time/, ClientApp/
│       └── patient-channel/
│           ├── README.md, Dockerfile
│           └── PatientChannelSimulator/   ASP.NET minimal API: Outbound/, Inbound/, Scenarios/, State/, ClientApp/
│
└── tests/
    ├── api/CareLoop.Tests/             xUnit, references Domain and Api
    │   ├── Domain/                     rule tests
    │   ├── Api/                        ingest, journeys, Fhir/ (with Samples/), Outbox/, Notifications/
    │   └── Fakes/                      FakeClock, InMemoryCaseRepository, TestData
    ├── simulators/
    │   ├── his/HisSimulator.Tests/
    │   └── patient-channel/PatientChannelSimulator.Tests/
    ├── ai_workflow/                    Python workflow tests (under src/ai_workflow/tests today)
    ├── contracts/                      contract and schema compatibility tests (reserved)
    └── integration/                    end-to-end synthetic journey tests (reserved)
```

File names inside folders are a starting point. Folder boundaries and dependency direction are fixed.

## Architecture rules

- **Dependency direction.** `CareLoop.Api` references `CareLoop.Domain`. Domain references nothing: no EF Core, no ASP.NET, no FHIR library, no JSON attributes.
- **Vertical slices.** Each file under `Features/` holds its endpoint, request, validation and handler together. Split a file only when it becomes unwieldy.
- **One write path.** Every change to a case goes through `ICaseRepository.SaveAsync`, which persists the source event, the updated case projection, the audit record and any outbox messages in **one transaction**. No other code writes case state.
- **One intake pipeline.** `EventIngestor` runs the same steps for every inbound event: validate, deduplicate, append, apply to the projection, audit. Deduplication uses a unique index on source system plus source event ID.
- **Reads go direct.** Read slices query `CareLoopDbContext` with no-tracking projections. Do not route reads through the repository.
- **FHIR is the door for systems of record.** Hospital systems (HIS, EMR, lab) talk to CareLoop only through `/fhir/...`. There is no public `POST /events`. Inbound FHIR mappers translate a resource into an internal `EventEnvelope` and hand it to `EventIngestor`. The FHIR slice contains no business logic.
- **Notification providers are the door for patient channels.** Reaching a patient, and hearing back from them, belongs to the enabled notification provider, not to the FHIR boundary. Outbound goes through `INotificationProvider`. Inbound (delivery reports and patient replies) arrives at `POST /notifications/inbound/{provider}` and is translated by that provider's `IInboundNotificationHandler` into `EventEnvelope`s for `EventIngestor`. A handler only verifies and translates; it contains no business logic.
- **The dashboard is the exception.** `src/web/` is CareLoop's own UI and uses the plain REST read endpoints.
- **Outbox for everything outbound.** Domain rules raise domain events. `OutboxDispatcher` delivers each to every `IDomainEventSubscriber` and tracks delivery per subscriber (`OutboxDelivery`), so subscribers retry independently and must be idempotent.
- **Interfaces only at real seams.** `ICaseRepository`, `IPathwayProvider`, `IClock`, `IAiProvider`, `INotificationProvider`, `IInboundNotificationHandler`, `IDomainEventSubscriber`, `IEventHandler`, `IFhirInboundMapper`. Do not add generic repositories or an interface per handler.
- **Simulators share no code with CareLoop.** They build and parse FHIR themselves, as a real hospital system would. Do not add project references between `src/simulators/` and `src/api/`.

## Domain model

**Case.** One actionable result equals one case. It holds source identifiers and references (not clinical data), the patient's display name and a second identifier such as the MRN (demographic, for staff screens; never shown to the coordinator role), pathway, clinician-reviewed urgency, stage, current owner and due time.

**Stages.**

| Stage | Meaning | Leaves when |
|---|---|---|
| Received | Result event ingested and deduplicated | An owner is assigned, or the case lands in the unassigned queue |
| ReviewPending | Awaiting clinician review of urgency and follow-up | A review event arrives |
| FollowUpActive | The parallel tracks are in progress | An outcome event is recorded |
| ClosureReview | Outcome evidence present, awaiting authorized decision | A closure event arrives with evidence or a documented exception |
| Closed | Final | Never |

The entry and exit rules above are a proposal still to be confirmed by the team. Keep them in the pathway definition so they can change without code edits.

**Parallel tracks.**

| Track | Statuses | Complete when |
|---|---|---|
| Notification | Sent, Delivered, Failed, Acknowledged, Understood | The patient acknowledges; delivery alone does not count |
| Consultation | Requested, OwnerProposed, Accepted, Rejected, OutcomeRecorded | The outcome is recorded |
| Referral | Sent, Accepted, Rejected | Accepted, which still is not completed care |
| Appointment | Scheduled, Attended, Missed | Attended, with a documented outcome |

`Escalated` is a flag on the case, not a stage.

**Invariants (enforce in Domain, test each one).**

- A case always has exactly one owner. Ownership transfer is two-step: propose, then accept. Until acceptance the previous owner stays accountable. Rejection returns the next action to the current owner. An unowned case goes to the facility lead queue.
- Transitions are guarded: who may perform it and what evidence it needs.
- A sent or accepted referral is never counted as completed care.
- An exception (unreachable, rejected, missed, declined, moved facility) never parks a case; it creates a next action with an owner and a deadline.
- Closure requires an authorized human event plus evidence or a documented exception, with a typed reason: treated, on surveillance with a next date, negative on confirmation, declined, transferred and accepted elsewhere, or untraceable after documented attempts.
- A notification is not an acknowledgement. Notifying someone never transfers ownership or stops a clock.

**Audit.** Every accepted event and coordination change appends an `AuditEvent` with source or actor, role, source and received timestamps, prior and new projection, reason and evidence reference.

## FHIR mapping

Assume **FHIR R4** and the Firely .NET SDK (`Hl7.Fhir.R4`). The FHIR version required by the Abu Dhabi Department of Health exchange standard has not been confirmed; ask before pinning.

Inbound:

| Event | FHIR resource | Interpretation |
|---|---|---|
| Screening result | `DiagnosticReport` with `Observation`s | A new report opens a case; the patient's display name, MRN, language and contact points come from its `Patient` (contained, or read from the source) |
| Clinical review | `Task` (review) | Completed, with priority as the reviewed urgency |
| Consultation | `ServiceRequest` plus `Task` | Task status: requested, accepted, rejected, completed |
| Referral | `ServiceRequest` plus `Task` | Same pattern |
| Appointment | `Appointment` | booked, fulfilled, noshow |
| Outcome | `Procedure` or follow-up `DiagnosticReport` | Becomes the evidence reference |
| Closure | Case `Task` set to completed, plus `Provenance` | Provenance names the authorizer; it counts only when it targets that case `Task` |
| Caregiver consent | `Consent` | `active` grants, `inactive`, `rejected` or a `deny` provision withdraws; the caregiver is a contained `RelatedPerson` |

Outbound:

| From CareLoop | FHIR resource |
|---|---|
| Escalation notice | `Task` |
| Handoff summary | `DocumentReference`, sent when a person approves it |
| Case status (also feeds the patient view) | `Task` via `GET /fhir/Task/{id}` |

Case to `Task` field mapping: handoff status to `Task.status`; pathway stage to `Task.businessStatus`; owner to `Task.owner`; who handed over to `Task.requester`; the result or referral to `Task.focus`; patient to `Task.for`; deadline to `Task.restriction.period`; rejection or closure reason to `Task.statusReason`. Keep all code mappings in `StatusMap`.

FHIR carries state; the engine wants events. Each inbound mapper must work out which transition a resource represents. Use the resource identifier plus its version (or last-updated time) as the source event ID so a resend of the same state is deduplicated.

Delivery reports and patient replies are not part of the FHIR boundary. They come back through the notification provider that sent the message (see "Events and notifications").

Endpoints: `POST /fhir/{resourceType}`, `PUT /fhir/{resourceType}/{id}` (the URL id must equal the body id), `GET /fhir/Task/{id}`, `GET /fhir/metadata`.

The sending system names itself in `meta.source`, using its key under `ExternalSystems:Systems`. A resource without it is refused with 400, and one from a system that is not configured there with 403. Replies go back to the system a case came from.

A contact point with `ContactPoint.system` `other` is reached through the simulator channel, for the patient and for a consented caregiver alike; `sms` and `email` are reserved for the real providers.

## Events and notifications

| Domain event | FHIR message out | Direct notification to |
|---|---|---|
| `NotificationRequested` | None | Patient |
| `HandoffProposed` | `Task` | Proposed owner |
| `HandoffSummaryApproved` | `DocumentReference` (exactly the approved text) | None |
| `HandoffRejected`, `ReferralRejected` | `Task` update | Current owner |
| `CallRequested` | `Task` | Current owner |
| `DeadlineApproaching` | None | Current owner |
| `EscalationRaised` | `Task` | Facility lead, then coordinator |
| `AppointmentScheduled` | None | Caregiver, only while the patient's consent is active |
| `CaseClosed` | `Task` completed | None |

Two subscribers exist: `FhirPublisher` and `NotificationRouter`.

Notification rules:

- Channels are **system configuration**, not recipient preference. `NotificationSender` sends on **every enabled provider** for which `CanReach(recipient)` is true. Redundancy is intended.
- Each send is recorded as its own `NotificationAttempt`. The notification is delivered if any channel delivers and failed only if all fail. An acknowledgement on any channel acknowledges the notification.
- Content is per channel: a provider with `AllowsSensitiveContent == false` (SMS) gets the SMS-safe template.
- Staff notifications carry no patient detail: "a case needs your action" plus a link into the HIS.
- Templates come from `Infrastructure/Templates/{language}/`. English only for now; keep everything language-keyed so Arabic can be added later.

```csharp
public interface INotificationProvider
{
    NotificationChannel Channel { get; }
    bool AllowsSensitiveContent { get; }
    bool CanReach(Recipient recipient);
    Task<SendResult> SendAsync(NotificationMessage message, CancellationToken ct);
}
```

```csharp
public interface IInboundNotificationHandler
{
    NotificationChannel Channel { get; }
    Task<IReadOnlyList<EventEnvelope>> TranslateAsync(InboundNotificationMessage message, CancellationToken ct);
}
```

```json
"Notifications": {
  "EnabledProviders": [ "Simulator" ],
  "PerEventOverrides": { },
  "Simulator": { "PatientChannelBaseUrl": "...", "StaffInboxBaseUrl": "..." }
}
```

A channel is two-way, so a provider has two halves:

- **Outbound**, `INotificationProvider`: sends the message.
- **Inbound**, `IInboundNotificationHandler`: understands what comes back on that channel (delivery, failure, acknowledgement, teach-back, call-me, or a reply such as a patient answering an SMS) and translates it into internal events. Each provider has its own format, so each has its own handler, reached at `POST /notifications/inbound/{provider}`.

Most of the patient channel lives in the patient-channel simulator, not in the API. The simulator receives notifications and then responds as a patient would for the scenario under test: delivers or fails, acknowledges, confirms understanding, asks for a call, or stays silent. The API knows only that the `Simulator` provider is enabled.

`SimulatorNotificationProvider` is the only enabled provider. It sends patient messages to the patient-channel simulator and staff messages to the HIS simulator's staff inbox; `SimulatorInboundNotificationHandler` receives what the patient-channel simulator sends back. The wire format between them (FHIR `CommunicationRequest` out, `Communication` back) is private to that provider. `SmsNotificationProvider` and `EmailNotificationProvider` are the production providers and are not being built yet: create them as documented stubs that throw `NotSupportedException`, so the seam exists and they can be implemented later without touching the sender or the router.

## Key interfaces

```csharp
public interface ICaseRepository
{
    Task<Case?> GetAsync(Guid caseId, CancellationToken ct);
    Task<Case?> FindBySourceAsync(string sourceSystem, string sourceResultId, CancellationToken ct);
    Task<bool> EventExistsAsync(string sourceSystem, string sourceEventId, CancellationToken ct);

    // Persists the event, the updated case projection, the audit record
    // and any raised domain events (outbox) in one transaction.
    Task SaveAsync(Case @case, SourceEvent appliedEvent, CancellationToken ct);
}
```

| Interface | Defined in | Implementations |
|---|---|---|
| `ICaseRepository` | `Domain/Cases` | `EfCaseRepository`, `InMemoryCaseRepository` (tests) |
| `IPathwayProvider` | `Domain/Pathways` | `JsonPathwayProvider` |
| `IClock` | `Domain/Time` | `SystemClock`, `FakeClock` (tests) |
| `IAiProvider` | `Api/Features/Summaries` | `NoOpAiProvider` (default), `AiWorkflowProvider`, `FoundryDirectProvider` |
| `INotificationProvider` | `Api/Features/Notifications` | Simulator, SMS (stub), Email (stub) |
| `IInboundNotificationHandler` | `Api/Features/Notifications` | Simulator; SMS and email when those providers are built |
| `IDomainEventSubscriber` | `Api/Features/Outbox` | `FhirPublisher`, `NotificationRouter` |
| `IEventHandler` | `Api/Features/Events` | Eight handlers, chosen by event type |
| `IFhirInboundMapper` | `Api/Features/Fhir` | One per inbound resource |

Never read the system time directly. Always use `IClock`, so deadline tests and the simulation replay can control time.

### Time in a live run

Scenarios span days and CareLoop measures deadlines in real time, so a live run is compressed at the edges, never inside the API:

- **API.** `Pathways:Directory` selects the pathway folder. `Infrastructure/Pathways/demo` holds the same definitions with every duration divided by 1800 (one pathway hour is two real seconds). Development and Docker Compose use it; the base settings use the real folder. A test asserts the two files differ only by that factor.
- **Simulators.** `Scenarios:RealSecondsPerScenarioHour` (default 2) paces the HIS simulator's steps and compresses the patient's `respondAfter`. It must match the demo pathway's factor.
- **Tests.** Journey tests use `FakeClock` with the real pathway folder and need neither.

Timestamps are always real. Do not add a fast or fake clock to the API outside tests.

## Technology

| Area | Choice |
|---|---|
| API | ASP.NET Core Web API on the current .NET LTS, minimal APIs, EF Core |
| Database | PostgreSQL (not yet confirmed; Azure SQL is the alternative. Keep EF Core code provider-neutral) |
| FHIR | Firely .NET SDK, R4 (version to confirm) |
| Background work | `BackgroundService` inside the API: `DeadlineWorker`, `OutboxDispatcher` |
| AI | `IAiProvider`; `NoOpAiProvider` is the default so everything runs with AI off. See [`docs/api-ai-workflow-handoff.md`](docs/api-ai-workflow-handoff.md) |
| Dashboard and simulator UIs | React, TypeScript, Vite |
| .NET tests | xUnit |
| Web tests | Vitest for unit, Playwright for end-to-end |
| AI workflow coordination | Python service in `src/ai_workflow/` (see its README); it proposes actions and the API decides. It also owns the handoff summary prompt and the Foundry call |
| Run | Docker Compose at the repository root |

Ask before adding any dependency not listed here.

## Documentation standards (required)

Documentation is part of the definition of done. Code without it is not finished.

### C# (all projects under `src/api/` and `src/simulators/`)

- Every `public` and `internal` type and member has XML documentation comments: classes, records, interfaces, enums and enum members, methods, properties, constructors.
- Use `<summary>` always; `<param>` for every parameter; `<returns>` for non-void methods; `<exception cref="...">` for every exception a method throws deliberately; `<remarks>` for anything non-obvious.
- For Domain rules, `<remarks>` must state **why** the rule exists (the safety or accountability reason), not only what it does.
- Use `<see cref="..."/>` to link related types, and `<inheritdoc/>` on interface implementations instead of duplicating text.
- Describe behaviour and intent. Do not restate the member name ("Gets the name" on `Name` is not acceptable).
- The repository-root `Directory.Build.props` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for every project and treats missing-comment warning `CS1591` as an error. Test projects (names ending in `.Tests`) suppress `CS1591` there; do not relax it elsewhere.
- Expose XML comments in the OpenAPI document so endpoint and contract descriptions appear there.

Example:

```csharp
/// <summary>
/// Decides whether a case may be closed.
/// </summary>
/// <remarks>
/// Closure is the point at which CareLoop stops watching a patient. It is therefore
/// allowed only on an authorized human decision backed by evidence or a documented
/// exception, so a case can never be closed by automation or by an unsupported claim.
/// </remarks>
public static class ClosureGate
{
    /// <summary>
    /// Evaluates a closure request against the pathway's evidence rules.
    /// </summary>
    /// <param name="case">The case the request applies to.</param>
    /// <param name="decision">The closure decision, including who authorized it and why.</param>
    /// <param name="pathway">The pathway definition supplying the evidence rules.</param>
    /// <returns>A result stating whether closure is allowed and, if not, the reason.</returns>
    public static ClosureResult Evaluate(Case @case, ClosureDecision decision, PathwayDefinition pathway)
    {
        // ...
    }
}
```

### TypeScript and React (`src/web/` and each simulator's `ClientApp/`)

- Every exported component, hook, function, type, interface and constant has a TSDoc comment (`/** ... */`).
- Components: describe what the component shows and when it is used. Document every prop on the props type, field by field.
- Hooks: describe what the hook does, its parameters with `@param`, what it returns with `@returns`, and any side effects such as network calls or polling.
- Functions: `@param`, `@returns`, and `@throws` where relevant. Add `@example` for anything non-obvious.
- Use `@remarks` to explain domain meaning, for example why a row is flagged as an exception.
- Each feature folder has an `index.ts` or `README.md` with a short note on what the feature is for.
- Enable strict TypeScript. Lint documentation with `eslint-plugin-tsdoc` and `eslint-plugin-jsdoc`, configured to require comments on exports; lint errors fail the build.
- Do not hand-edit the generated API client in `src/web/src/api/`; regenerate it from the OpenAPI document in `contracts/openapi/careloop.json`.
- All user-visible strings come from `locales/en.json` through the i18n layer. No hard-coded UI text. Use direction-neutral (logical) CSS so right-to-left can be enabled later.

Example:

```tsx
/** Props for {@link ExceptionRow}. */
export interface ExceptionRowProps {
  /** The exception to display, as returned by `GET /exceptions`. */
  item: ExceptionItem;
  /** Called when the user opens the case timeline for this row. */
  onOpen: (caseId: string) => void;
}

/**
 * One row in the exception queue.
 *
 * @remarks
 * A row appears here only while the case has an unresolved exception
 * (overdue, rejected, unreachable, unacknowledged or unassigned). It shows
 * the single next action and who is accountable for it.
 */
export function ExceptionRow({ item, onOpen }: ExceptionRowProps) {
  // ...
}
```

### Project documentation

- Each project folder (`src/api/`, `src/web/`, `src/ai_workflow/`, `src/simulators/his/`, `src/simulators/patient-channel/`) has a `README.md`: what it is, how to run it, how to test it, and its configuration.
- The root `README.md` explains the whole system and how to start it with Docker Compose.

## Testing

- Every Domain rule and invariant has unit tests, including the failure cases (premature closure, unauthorized closure, duplicate event, rejected handoff).
- Every FHIR mapper has a test against a sample resource in `tests/.../Fhir/Samples/`.
- `EventIngestor` has an idempotency test: replaying the same source event produces one case and one event.
- Outbox: a failing subscriber does not block the others and is retried.
- Notifications: sends go to all enabled reachable channels; SMS gets the SMS-safe template; delivered-if-any and failed-if-all are both covered.
- Journey tests cover the four scenarios in `src/simulators/scenarios/`.
- End-to-end (Playwright): one successful journey and one broken handoff.
- Run the relevant tests after every change and report failures plainly. Do not weaken or delete a test to make it pass.

## Build order

Work in this order. Finish and test each step before starting the next.

1. **Scaffold.** Solution, the three API projects, `src/web/`, both simulators, Docker Compose, CI-ready build with documentation enforcement on.
2. **Domain.** Cases, tracks, rules, pathway definition, domain events, with full unit tests.
3. **Persistence and intake.** Schema, `EfCaseRepository`, `EventIngestor`, the eight handlers. Tests call `EventIngestor` directly with internal envelopes. The engine must be demonstrable before any FHIR exists.
4. **Deadlines and escalation.** `DeadlineWorker`, escalation ladder, exception flags.
5. **Read endpoints.** Cases, timeline, exceptions, metrics, with OpenAPI.
6. **FHIR boundary.** Endpoints, then inbound mappers one resource at a time starting with `DiagnosticReport`, then outbound mappers.
7. **Outbox and notifications.** Dispatcher, `FhirPublisher`, `NotificationRouter`, `NotificationSender`, simulator provider, templates.
8. **Dashboard.** Exception queue, case timeline, metrics.
9. **Simulators.** HIS first, then patient channel, each with its scenario runner; UIs last.
10. **AI handoff summary.** Draft, approval and rejection behind `IAiProvider`; see [`docs/api-ai-workflow-handoff.md`](docs/api-ai-workflow-handoff.md).
11. **Scenarios and simulation.** Scenario scripts, cohort, the with-and-without-escalation comparison, end-to-end tests.

## Working agreements

- Make small, focused changes. Do not restructure folders or rename public contracts without asking.
- When something here is ambiguous or marked unconfirmed, ask rather than guess. Open questions right now: FHIR version, database choice, stage entry and exit rules, clinical deadlines.
- Do not invent clinical content: result categories, urgency levels, timeframes or closure criteria must come from the pathway definition file, with values marked as placeholders.
- Keep secrets out of the repository. Use environment variables and provide a `.env.example`.
- Report honestly what was built, what was tested and what was not.
- Keep [`docs/workstreams.md`](docs/workstreams.md) current. When a change removes a workstream's last `TODO(WS-n)` tag and un-skips its last test, set that workstream's **Status** to `Done` in the summary table in the same change. When you finish a design-gap item, mark it `Done` in its workstream's section. Never mark something `Done` that still has a stub, a skipped test or a failing test; if later work reopens a workstream, set it back to `Open`.
