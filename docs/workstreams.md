# Workstreams

Ten workstreams build CareLoop in parallel. This file says, for each one, which files it owns, what it depends on and
which stubs it implements. The earlier component register, which maps each component to its user stories and functional
requirements, is kept in [`component-register.md`](component-register.md). Find your stubs with:

```bash
grep -rn "TODO(WS-4)" --include="*.cs" --include="*.ts" --include="*.tsx" .
```

A second list, **Design gaps**, appears under some workstreams. These come from
[`ui-design-gap-analysis.md`](ui-design-gap-analysis.md) (items C1 to C13), which compares the screen designs with the
code. They are additive. The seven decisions in that document's section 5 were taken on 13 November 2026.

Rules of the road:

- Edit only the files your workstream owns. To change someone else's file, or anything under **Shared**, agree it first.
- A stub's XML or TSDoc comment is its specification. If you need to change a public contract, ask before you do.
- When you implement a stub, remove its `TODO(WS-n)` tag and un-skip its tests. Never weaken or delete a test.
- A path ending in `/**` means the folder and everything in it. A more specific line overrides a broader one.

| WS | Name | Depends on |
|---|---|---|
| 1 | Domain rules | nothing |
| 2 | Persistence and intake | WS1 |
| 3 | FHIR boundary | WS2 (ingestor and envelope contracts) |
| 4 | Outbox and notifications | WS1 (domain events), WS2 (outbox tables) |
| 5 | Deadlines and escalation | WS1, WS2 |
| 6 | Read endpoints and dashboard | WS2 (schema) |
| 7 | HIS simulator | WS3 (FHIR contract and codes) |
| 8 | Patient-channel simulator | WS3 (FHIR contract and codes), WS4 (notification delivery) |
| 9 | AI handoff summary | WS2, WS6 |
| 10 | Scenarios and end-to-end tests | all of the above |

Because the contracts already exist, every workstream can start now against the stubs and fakes
(`FakeClock`, `InMemoryCaseRepository`); "depends on" means "needs it to run end to end".

---

## WS1 Domain rules

**Owns**

- `src/api/CareLoop.Domain/**` (except the two deadline files listed under WS5)
- `src/api/CareLoop.Api/Infrastructure/Pathways/**`
- `tests/api/CareLoop.Tests/Domain/**` (except `DeadlinePolicyTests.cs`)

**Stubs to implement**

- `Cases/Case.cs`: every state-changing method (`RecordReview`, `MoveTo`, `SetNextAction`, `ProposeOwner`, `AcceptOwnership`, `RejectOwnership`, `AssignOwner`, the four `Begin...` methods, `FlagException`, `ClearException`, `Escalate`, `ResolveEscalations`, `RecordOutcomeEvidence`, `Close`)
- `Tracks/*Track.cs`: `Advance`, `IsComplete`, and `NotificationTrack.RecordAttempt`
- `Rules/TransitionRules.cs`, `Rules/OwnershipRules.cs`, `Rules/ClosureGate.cs`
- `Infrastructure/Pathways/JsonPathwayProvider.cs`
- `Infrastructure/Pathways/breast-screening.json`: replace placeholders only with values a clinical adviser has approved
- `Infrastructure/Pathways/demo/breast-screening.json`: keep it equal to the file above with every duration divided by 1800 (`DemoPathwayFileTests` enforces this)

**Design gaps (follow-up; WS1 is otherwise complete)**

- C2: add optional `citation` to `DeadlineDefinition` and `ClosureEvidenceRule`, a `changeLog` list on `PathwayDefinition`, and an `ownerRole` per next action, for the Programmes screen. Mirror in `demo/breast-screening.json`. Done.
- C10: `Confidence` on `NotificationTrack`, set by a new `NotificationUpdateKind.ConfidenceRated`. Done.
- C11: `nurse-navigator`, `receiving-clinician` and `lab` in the pathway's `allowedRoles` and `closureRules.authorizedRoles`, and in `OwnershipRules` (`MayOwn` reads the owning roles from the pathway). Done.
- C12: replace `respond-to-handoff` with `answer-consultation` and `respond-to-referral` deadlines, placeholders. Done.
- C13: `RecipientKind.Caregiver` and a `Consent` record on `Case` (granted, withdrawn, evidence reference). Done.

## WS2 Persistence and intake

**Owns**

- `src/api/CareLoop.Api/Features/Events/**`
- `src/api/CareLoop.Api/Contracts/Events/**`
- `src/api/CareLoop.Api/Infrastructure/Persistence/**` (except `Summaries/` and `Configurations/HandoffSummaryRecordConfiguration.cs`)
- `tests/api/CareLoop.Tests/Api/EventIngestorTests.cs`, `EventHandlerTests.cs`, `EfCaseRepositoryTests.cs`
- `tests/api/CareLoop.Tests/Fakes/**`
- `.config/dotnet-tools.json`

**Stubs to implement**

- `Features/Events/EventIngestor.cs`
- `Features/Events/Handlers/*.cs` (nine handlers; `ConsentHandler` was added for C13)
- `Infrastructure/Persistence/EfCaseRepository.cs`
- Audit record building in `InMemoryCaseRepository` once the projection format is decided

**Design gaps**

- C10: `ConfidenceRated` kind and value on `NotificationUpdatePayload`, handled in `NotificationUpdateHandler`. Done.
- C12: set `answer-consultation` and `respond-to-referral` as next actions in `ConsultationUpdateHandler` and `ReferralUpdateHandler`. Done.
- C13: caregiver consent is its own event, `CaseEventType.ConsentRecorded`, carried by `ConsentPayload` (granted or withdrawn, caregiver contact point, consent reference) and applied by `ConsentHandler`; persisted on the case by migration `CaregiverConsentAndConfidence`. Done.

## WS3 FHIR boundary

**Owns**

- `src/api/CareLoop.Api/Features/Fhir/**`
- `src/api/CareLoop.Api/Infrastructure/Fhir/**`
- `tests/api/CareLoop.Tests/Api/Fhir/**`

**Stubs to implement**

- `Features/Fhir/FhirEndpoints.cs`, `GetCapabilityStatement.cs`, `StatusMap.cs`
- `Features/Fhir/Inbound/*.cs` (six mappers, starting with `DiagnosticReportMapper`)
- `Features/Fhir/Outbound/*.cs` (three mappers and `FhirPublisher`)
- `Infrastructure/Fhir/FhirHttpClient.cs`

**Design gaps**

- C10: map `Communication.category` `confidence-rating` to `NotificationUpdateKind.ConfidenceRated` in `StatusMap.ToNotificationUpdateKind`, and carry the 1 to 5 value into `NotificationUpdatePayload.Confidence` (the handler refuses a missing or out-of-range value).
- C13: add a seventh inbound mapper, `ConsentMapper`, that turns a FHIR `Consent` resource into a `ConsentRecorded` envelope with a `ConsentPayload`: `status` `active` is `Granted` (with the caregiver's contact point read from the resource), `inactive` or `rejected` is `Withdrawn`; the resource identifier plus version is the source event ID. Register it with the other mappers.

## WS4 Outbox and notifications

**Owns**

- `src/api/CareLoop.Api/Features/Outbox/**`
- `src/api/CareLoop.Api/Features/Notifications/**`
- `src/api/CareLoop.Api/Infrastructure/Notifications/**`
- `src/api/CareLoop.Api/Infrastructure/Templates/**`
- `tests/api/CareLoop.Tests/Api/Outbox/**`, `tests/api/CareLoop.Tests/Api/Notifications/**`

**Stubs to implement**

- `Features/Outbox/OutboxDispatcher.cs`
- `Features/Notifications/NotificationRouter.cs`, `RecipientResolver.cs`, `NotificationSender.cs`, `TemplateRenderer.cs`
- `Features/Notifications/ReceiveInboundNotification.cs` (the provider callback endpoint)
- `Infrastructure/Notifications/SimulatorNotificationProvider.cs`, `SimulatorInboundNotificationHandler.cs`
- Leave `SmsNotificationProvider` and `EmailNotificationProvider` as stubs: they are out of scope for now
- Set `Outbox:Enabled` to `true` in `appsettings.json` when the dispatcher works

**Design gaps**

- C10: parse the confidence value in `SimulatorInboundNotificationHandler`.
- C13: `RecipientResolver` sends a caregiver with active consent the appointment date, time and place only; a caregiver template under `Templates/en/`; each send is a `NotificationAttempt`; withdrawal stops sends. `EmailNotificationProvider` stays a stub.

## WS5 Deadlines and escalation

**Owns**

- `src/api/CareLoop.Api/Worker/**`
- `src/api/CareLoop.Domain/Rules/DeadlinePolicy.cs`, `src/api/CareLoop.Domain/Rules/DeadlineStatus.cs`
- `tests/api/CareLoop.Tests/Domain/DeadlinePolicyTests.cs`, `tests/api/CareLoop.Tests/Api/DeadlineWorkerTests.cs`

**Stubs to implement**

- `Rules/DeadlinePolicy.cs`: `ComputeDueAt`, `Classify`, `NextEscalationStep`
- `Worker/DeadlineWorker.cs`: `ExecuteAsync`, `SweepAsync`
- Set `DeadlineWorker:Enabled` to `true` in `appsettings.json` when the worker works

## WS6 Read endpoints and dashboard

**Owns**

- `src/api/CareLoop.Api/Features/Cases/**`, `Features/Exceptions/**`, `Features/Metrics/GetMetrics.cs`, `Features/Common/**`
- `src/api/CareLoop.Api/Contracts/Cases/**`, `Contracts/Exceptions/**`, `Contracts/Common/**`, `Contracts/Metrics/MetricsResponse.cs`
- `contracts/openapi/**` (generated by the API build; commit it when routes or contracts change)
- `tests/api/CareLoop.Tests/Api/ReadEndpointsTests.cs`
- `src/web/**` (except `src/web/tests/e2e/**` and `src/web/playwright.config.ts`; the dashboard is a starter today, see `src/web/README.md`)

**Stubs to implement**

- `ListCases`, `GetCase`, `GetTimeline`, `GetExceptions`, `GetMetrics`
- `src/web/src/features/exceptions`, `cases`, `metrics`: replace each placeholder page
- Do not hand-edit `src/web/src/api/schema.d.ts`; run `corepack yarn generate:api`

**Design gaps**

- C1: `GET /programmes` and `GET /programmes/{pathwayId}` with a `ProgrammeResponse` contract (new `Features/Programmes/`, `Contracts/Programmes/`).
- C3: `SourceSystem` on `TimelineEntryResponse`.
- C4: `facilityId` filter on `GET /cases`, refusal of case reads outside the caller's facility, an audit event per read, and `X-Demo-Facility` in `DemoRoles` (shared file; agree first).
- C5: time-to-step medians with denominators, understanding rate and closures by reason on `MetricsResponse` (with WS10 for `SimulationArmResponse`).
- C9: build screens 1 to 4 of the design in `src/web/`, against the endpoints above. Read-only except the AI summary draft and approve controls on the case screen (decision 4). Derive the ten display labels from stage plus track statuses (decision 1).
- C10: `confidenceRate` on `MetricsResponse`.
- C11: add `nurse-navigator`, `receiving-clinician` and `lab` to `DemoRoles.HumanRoles` (shared file; agree first).

## WS7 HIS simulator

**Owns**

- `src/simulators/his/**`, `tests/simulators/his/**`

**Stubs to implement**

- `Outbound/`: the resource builders, `CareLoopFhirClient`, `OutboundEventSender`, clinician action endpoints
- `Inbound/`: `InboundFhirReceiver`, FHIR, staff inbox and message log endpoints
- `Scenarios/`: `ScenarioLoader`, `ScenarioRunner`, scenario endpoints
- `State/SimulatorStore.cs`
- `ClientApp/src/features/*`: scenario runner, staff inbox, FHIR log, clinician actions

**Design gaps**

- C6: manual actions `POST /actions/consultation-request`, `/actions/consultation-answer`, `/actions/referral`, `/actions/appointment`, wired to the existing builders, with forms on the clinician actions page (screens 5 and 6).
- C11: let `ActingStaff` take the roles `nurse-navigator`, `receiving-clinician` and `lab`, and offer them on the clinician actions page.
- Decision 4: AI summaries are approved in the CareLoop web app. The HIS simulator only receives the approved `DocumentReference`; no CareLoop endpoint other than `/fhir` is called.

## WS8 Patient-channel simulator

**Owns**

- `src/simulators/patient-channel/**`, `tests/simulators/patient-channel/**`

**Stubs to implement**

- `Inbound/`: `CommunicationRequestReceiver`, `CommunicationRequestMapper`, `DeliverySimulator`, `AutomaticResponder`, and the inbox, patient behaviour, message log and case status endpoints
- `Outbound/`: the five `Communication` builders, `CareLoopFhirClient`, `PatientResponseSender`, `CaseStatusReader`
- `Scenarios/`: `ScenarioLoader`, `ScenarioRunner`, scenario endpoints
- `State/SimulatorStore.cs`
- `ClientApp/src/features/*`: inbox, case status, patient behaviours, scenario runner, message log

**Design gaps**

- C7: on the `Sms` channel accept an inbound reply of `1` as acknowledge and `2` as call-me (screen 9).
- C10: a confidence rating action (1 to 5) on the inbox and a `Communication` builder with category `confidence-rating`.
- C13: an `Email` value in `InboxChannel` so caregiver messages are shown, not sent.

## WS9 AI handoff summary

**Owns**

- `src/api/CareLoop.Api/Features/Summaries/**`
- `src/api/CareLoop.Api/Contracts/Summaries/**`
- `src/api/CareLoop.Api/Infrastructure/Ai/**`
- `src/api/CareLoop.Api/Infrastructure/Persistence/Summaries/**`, `Infrastructure/Persistence/Configurations/HandoffSummaryRecordConfiguration.cs`
- `tests/api/CareLoop.Tests/Api/HandoffSummaryTests.cs`

**Stubs to implement**

- `Features/Summaries/DraftHandoffSummary.cs`, `ApproveSummary.cs`
- `Infrastructure/Ai/AzureOpenAiProvider.cs` (ask before adding an SDK package; the stub uses plain `HttpClient`)
- `NoOpAiProvider` already works and stays the default

## WS10 Scenarios and end-to-end tests

**Owns**

- `src/simulators/scenarios/**`
- `src/api/CareLoop.Api/Features/Metrics/RunSimulation.cs`
- `src/api/CareLoop.Api/Contracts/Metrics/SimulationRequest.cs`, `SimulationResponse.cs`, `SimulationArmResponse.cs`
- `tests/api/CareLoop.Tests/Api/Journeys/**`, `tests/api/CareLoop.Tests/Api/SimulationTests.cs`
- `src/web/tests/e2e/**`, `src/web/playwright.config.ts`

**Stubs to implement**

- `RunSimulation`: the with-and-without-escalation comparison
- The four journey test classes and the two Playwright specs
- Changes to `scenario.schema.json` must be agreed with WS7 and WS8, whose scenario records mirror it

---

**Design gaps**

- C8: scenarios for call-me then call-back, understand after a reminder, consultation rejected, missed appointment then rebook, closure `Declined`, and closure `Untraceable` with documented attempts.
- C5: give `SimulationArmResponse` the same measures as `MetricsResponse` so the escalation-off comparison is like for like.
- C10: a `rate-confidence` patient behaviour in `scenario.schema.json` (agree with WS7 and WS8).
- C13: a scenario with a consented caregiver and one where consent is withdrawn.

## Shared (change by agreement)

These files are touched by several workstreams. Keep changes small and announce them.

- `AGENTS.md`, `README.md`, `docs/workstreams.md`
- `.editorconfig`, `.gitignore`, `.dockerignore`, `.env.example`, `docker-compose.yml`
- `.github/**`
- `CareLoop.sln`, `Directory.Build.props`, `global.json`, `src/api/CareLoop.Api/Dockerfile`, `src/api/README.md`, every `*.csproj` under `src/api/`
- `src/api/CareLoop.Api/Program.cs`, `appsettings.json`, `appsettings.Development.json`, `Properties/launchSettings.json`
- `src/api/CareLoop.Api/Infrastructure/DependencyInjection.cs` (one method per area; edit only your own method)
- `src/api/CareLoop.Api/Infrastructure/Time/**`, `Infrastructure/Auth/**`
- `tests/api/CareLoop.Tests/Api/WorkingInfrastructureTests.cs`, `ExternalSystemsConfigurationTests.cs`

## Contract points to settle between workstreams

The scaffold made these choices or left them open. Settle each one before both sides build on it.

| Topic | Between | State |
|---|---|---|
| `PUT` route shape | WS3, WS7, WS8 | The API maps `PUT /fhir/{resourceType}/{id}` (standard FHIR update). `AGENTS.md` writes `PUT /fhir/{resourceType}`. Confirm. |
| `Communication.category` codes | WS4, WS8 | Both sides use system `https://careloop.invalid/fhir/CodeSystem/communication-category`. WS8 proposes codes `delivery-status`, `acknowledgement`, `teach-back`, `call-me`; `StatusMap` must match. |
| External system identifiers | WS2, WS3, WS7 | `ExternalSystems:Systems` is keyed by system identifier, and the key must equal the source system identifier that system sends. The development settings use `his-simulator`; the HIS simulator does not yet send an agreed identifier. |
| Scenario patient behaviour | WS7, WS8, WS10 | A scenario's steps are hospital-system events only. How a patient reacts is `patients[].behaviour` (`delivery`, `response`, `respondAfter`, `respondToNotification`), applied by the patient-channel simulator when a notification arrives. Cohort runs need a way to give each generated patient their scenario's behaviour. |
| Time in a live run | WS1, WS5, WS7, WS8, WS10 | CareLoop runs on real time. A live run uses the demo pathway folder (`Pathways:Directory`), whose deadlines are divided by 1800, and both simulators pace scenarios with `Scenarios:RealSecondsPerScenarioHour` = 2. The three figures must stay equal. End-to-end tests should assert outcomes, not exact times, and leave headroom for slow machines. |
| Simulator provider wire format | WS4, WS8 | Private to the simulator provider: a FHIR `CommunicationRequest` to the patient-channel simulator, and a FHIR `Communication` back to `POST /notifications/inbound/simulator`. Agree the `Communication.category` codes and how the notification attempt identifier is carried. |
| Patient replies beyond acknowledgement | WS1, WS4, WS8 | A reply such as a patient accepting an appointment has no event or track status yet. Decide which replies exist and what each one means for the case before a handler translates them. |
| Simulated marker on FHIR resources | WS7, WS8 | The simulators mark the resources they send (the API test samples use `meta.tag` in system `.../data-origin` with code `simulated`; WS8 planned `meta.security` `HTEST`; pick one). CareLoop neither requires nor emits a marker. |
| Staff notification transport | WS4, WS7 | The HIS simulator offers both `POST /fhir/Task` and a plain `POST /staff-inbox/notifications`. Decide which `SimulatorNotificationProvider` uses and drop the other. |
| Urgency on the review `Task` | WS3, WS7 | `AGENTS.md` says `Task.priority`. Placeholder urgency codes do not fit the FHIR priority value set; decide between priority and an extension. |
| Nine event types | WS2, WS3 | Delivery reports and patient responses share one event, `NotificationUpdated`. Caregiver consent is its own event, `ConsentRecorded` (C13), so there are exactly nine source events and handlers. |
| Internal events | WS2, WS5, WS9 | `DeadlineElapsed` and `HandoffSummaryApproved` are saved through `ICaseRepository.SaveAsync` directly, not through a handler. |
| Rejected events | WS1, WS2 | A refused event is saved with `Disposition = Rejected`, so the attempt is audited through the same write path. |
| Stage model | WS1, WS6 | Settled (decision 1): five stages in the Domain; the design's ten stages are display labels the web app derives from stage plus track statuses. |
| Patient appointment confirmation | WS1, WS4, WS8 | Settled (decision 2): dropped. "I will come" is an acknowledgement; a patient reply never changes appointment status. |
| Confidence rating | WS1, WS2, WS3, WS4, WS6, WS8, WS10 | Settled (decision 3): build it (C10). `NotificationUpdateKind.ConfidenceRated` with a 1 to 5 value; `Communication.category` `confidence-rating`. |
| Where the AI summary is approved | WS6, WS7, WS9 | Settled (decision 4): the CareLoop web app case screen drafts and approves; the HIS simulator receives the approved `DocumentReference` only. |
| Roles | WS1, WS6, WS7 | Settled (decision 5): add `nurse-navigator`, `receiving-clinician` and `lab` (C11). `DemoRoles` is shared; WS6 edits it after announcing. |
| Consultation reply deadline | WS1, WS2 | Settled (decision 6): `answer-consultation` and `respond-to-referral` replace `respond-to-handoff`; both placeholders (C12). |
| Caregiver and consent | WS1, WS2, WS4, WS8 | Settled (decision 7): build it (C13). `RecipientKind.Caregiver`, a `Consent` record on the case, caregiver gets date, time and place only, shown in the patient-channel simulator's `Email` channel; real email stays out of scope. |
