# Workstreams

Ten workstreams build CareLoop in parallel. This file says, for each one, which files it owns, what it depends on and
which stubs it implements. The earlier component register, which maps each component to its user stories and functional
requirements, is kept in [`component-register.md`](component-register.md). Find your stubs with:

```bash
grep -rn "TODO(WS-4)" --include="*.cs" --include="*.ts" --include="*.tsx" .
```

A second list, **Design gaps**, appears under some workstreams. These come from
[`ui-design-gap-analysis.md`](ui-design-gap-analysis.md) (items C1 to C21), which compares the screen designs with the
code. They are additive. The decisions in that document's section 5 (1 to 7 and 12 to 20) were taken on 13 November 2026.

Rules of the road:

- Edit only the files your workstream owns. To change someone else's file, or anything under **Shared**, agree it first.
- A stub's XML or TSDoc comment is its specification. If you need to change a public contract, ask before you do.
- When you implement a stub, remove its `TODO(WS-n)` tag and un-skip its tests. Never weaken or delete a test.
- A path ending in `/**` means the folder and everything in it. A more specific line overrides a broader one.

| WS | Name | Depends on | Status |
|---|---|---|---|
| 1 | Domain rules | nothing | Done |
| 2 | Persistence and intake | WS1 | Done |
| 3 | FHIR boundary | WS2 (ingestor and envelope contracts) | Done |
| 4 | Outbox and notifications | WS1 (domain events), WS2 (outbox tables) | Open |
| 5 | Deadlines and escalation | WS1, WS2 | Open |
| 6 | Read endpoints and dashboard | WS2 (schema) | Open |
| 7 | HIS simulator | WS3 (FHIR contract and codes) | Open |
| 8 | Patient-channel simulator | WS3 (FHIR contract and codes), WS4 (notification delivery) | Open |
| 9 | AI handoff summary | WS2, WS6 | Open |
| 10 | Scenarios and end-to-end tests | all of the above | Open |

**Status** is `Done` or `Open`. A workstream is `Done` when none of its `TODO(WS-n)` tags and none of its skipped
tests (`Skip = "WSn: ..."`, `it.todo`) remain and its tests pass. Design gaps listed under a workstream are follow-up
work and are tracked item by item in that workstream's section, not in this column. Whoever finishes a workstream's
last stub updates this column in the same change.

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
- C15: `AcknowledgedBy` and `AcknowledgedAt` on `Escalation`; `Case.AcknowledgeEscalation` accepts only the role the latest open escalation went to, refuses a closed case or a second acknowledgement, and never touches exceptions, owner, next action or `DueAt`. Internal event `CaseEventType.EscalationAcknowledged` (102) carries it through the single write path. Done.
- Decision 18: `respond-to-referral` 4 hours, `answer-consultation` 1 day and `call-patient` 2 hours in both pathway files, still unapproved; the citation text says durations are clock time, not working hours. Done.
- C20: `PatientDisplayName` and `PatientSecondaryIdentifier` on `Case`, demographic only. Done.
- C21: `EvidenceKindDefinition` list on `PathwayDefinition` (kind, `clinicallyApproved`, optional `nextAction`: `treatment-plan` starts `start-treatment`, `treatment-started` and `surveillance-started` return to `decide-closure`); `JsonPathwayProvider` fails on missing or duplicate kinds; `TransitionRules` accepts `OutcomeRecorded` in `ClosureReview` without leaving the stage. Done. Migrations were scoped by hand because WS9's pending `HandoffSummaries` columns have no migration yet, and were not run against PostgreSQL.
- Decision 19: a pure function that derives one of the ten display labels from stage plus track statuses, for WS6 to return on reads and in metrics. Done (`CaseDisplayLabels.From(case, pathway)`).

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
- C15: persist the escalation acknowledgement (migration). Done; saved under the internal event `EscalationAcknowledged`.
- C20: `PatientDisplayName` and `PatientSecondaryIdentifier` on `ScreeningResultPayload`, copied to the case by `ScreeningResultHandler`; migration. Done.
- C21: `OutcomeHandler` rejects an `EvidenceKind` the pathway does not declare (saved with `Disposition = Rejected`). Done; the case keeps the latest `OutcomeEvidenceKind`.

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

- C10: map `Communication.category` `confidence-rating` to `NotificationUpdateKind.ConfidenceRated` in `StatusMap.ToNotificationUpdateKind`, and carry the 1 to 5 value into `NotificationUpdatePayload.Confidence` (the handler refuses a missing or out-of-range value). Done on the FHIR side: `StatusMap.ToConfidence` reads the value from the first `payload.contentString`; WS4's `SimulatorInboundNotificationHandler` has to call it, since patient replies do not cross the FHIR boundary.
- C13: add a seventh inbound mapper, `ConsentMapper`, that turns a FHIR `Consent` resource into a `ConsentRecorded` envelope with a `ConsentPayload`: `status` `active` is `Granted` (with the caregiver's contact point read from the resource), `inactive` or `rejected` is `Withdrawn`; the resource identifier plus version is the source event ID. Register it with the other mappers. Done: the caregiver is a contained `RelatedPerson` named in `provision.actor`, and the consent names its screening result through the `screening-result` extension.
- C20: `DiagnosticReportMapper` reads the patient's display name and MRN from the referenced FHIR `Patient` (contained or resolved through `FhirHttpClient`) into `ScreeningResultPayload`; a sample with a `Patient` in `Fhir/Samples/`. Done: a contained `Patient` (`subject` = `#id`) is read directly; a literal `Patient/{id}` is read from the source system when it is configured, and otherwise the case opens without demographics. `Patient.json` and the contained patient in `DiagnosticReport.json` are the samples.
- C21: `Procedure` and follow-up `DiagnosticReport` mappers set `OutcomePayload.EvidenceKind` to one of the pathway's declared kinds; agree the FHIR code to kind mapping in `StatusMap`. Done: the kind is the code in `https://careloop.invalid/fhir/CodeSystem/evidence-kind` on `Procedure.code` or the follow-up report's `code`, passed through unchanged; WS7 sends the pathway's kind names there.

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

- C10 (decision 3): in `SimulatorInboundNotificationHandler`, map `Communication.category` `confidence-rating` to `NotificationUpdateKind.ConfidenceRated` and the 1 to 5 value into `NotificationUpdatePayload.Confidence`, by calling WS3's `StatusMap.ToNotificationUpdateKind` and `StatusMap.ToConfidence`. A rating is a measure, never an acknowledgement. The WS1 to WS3 parts are done.
- C13 (decision 7): `RecipientResolver` sends a caregiver with active consent the appointment date, time and place only, never the result. Add a caregiver template under `Templates/en/`, with a standard and an SMS-safe variant. Record each send as a `NotificationAttempt`. Check `Consent.IsActive` when the message is sent, not only when the event was raised, because the outbox may deliver after a withdrawal; a withdrawal stops every later send. `SimulatorNotificationProvider` marks the `CommunicationRequest` as a caregiver message on the `Email` channel, so the patient-channel simulator files it in its Email inbox (WS8). `EmailNotificationProvider` stays a stub. `Consent`, `RecipientKind.Caregiver`, `ConsentHandler` and `ConsentMapper` are done. **Blocked:** scheduling an appointment raises no domain event and the appointment payload carries no place (see "Caregiver appointment message" in the contract points below).
- C15 (decision 13): each `EscalationRaised` staff notice says who acknowledged the previous level, if anyone (WS5 sends the event). This needs a field on `EscalationRaised` (WS1) and a placeholder in the staff template. Staff notices still carry no patient detail. See "Acknowledgement in escalation notices" in the contract points below.
- Decision 2: a patient reply never changes appointment status. "I will come" arrives as `acknowledgement` and is translated to `Acknowledged` only.
- Open contract points to settle before building: "Staff notification transport" (the HIS simulator already reads staff notices from FHIR `Task` codes) and "Simulator provider wire format" (how the notification attempt identifier and the recipient kind are carried).

## WS5 Deadlines and escalation

**Owns**

- `src/api/CareLoop.Api/Worker/**`
- `src/api/CareLoop.Domain/Rules/DeadlinePolicy.cs`, `src/api/CareLoop.Domain/Rules/DeadlineStatus.cs`
- `tests/api/CareLoop.Tests/Domain/DeadlinePolicyTests.cs`, `tests/api/CareLoop.Tests/Api/DeadlineWorkerTests.cs`

**Stubs to implement**

- `Rules/DeadlinePolicy.cs`: `ComputeDueAt`, `Classify`, `NextEscalationStep`
- `Worker/DeadlineWorker.cs`: `ExecuteAsync`, `SweepAsync`
- Set `DeadlineWorker:Enabled` to `true` in `appsettings.json` when the worker works

**Design gaps**

- C15: an acknowledged escalation is still overdue. The sweep keeps escalating up the ladder on the pathway's `afterOverdue` and includes who acknowledged the previous level in each `EscalationRaised` notice; acknowledgement never stops the clock. `EscalationRaised` has no field for the acknowledgement yet; see "Acknowledgement in escalation notices" in the contract points below.
- C21: `ComputeDueAt` handles the `start-treatment` deadline like any other next action; no special case.
- C12 and decision 18: `answer-consultation` (1 day), `respond-to-referral` (4 hours) and `call-patient` (2 hours) are ordinary next-action deadlines. Durations are clock time: add no working-hours calendar, which stays future work.

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
- C4: `facilityId` filter on `GET /cases`, refusal of case reads outside the caller's facility, an audit event per read, and `X-Demo-Facility` in `DemoRoles` (shared file; agree first). Added (decision 12): a read outside the caller's team requires a `reason` query parameter, stored in the same audit entry as the view.
- C5: the screen 3 measures on `MetricsResponse`, each with its denominator (with WS10 for `SimulationArmResponse`): time-to-step medians, understanding rate, closures by reason, cases reaching each display label (decision 19), referral accepted and attended, appointments attended, treatment started by its deadline and time to care started (both from the C21 evidence kinds: denominator is cases with a `treatment-plan` outcome, numerator a `treatment-started` outcome before the `start-treatment` due time).
- C9: build screens 1 to 4, 17 and 18 of the design in `src/web/` (decision 17), against the endpoints above. Read-only except: the handoff summary screen drafts, edits on approval and rejects (decisions 4 and 10); the escalation screen acknowledges (C15); the other-cases card for clinician roles (C19). Show the display label the API returns (decision 19); do not recompute it.
- C10: `confidenceRate` on `MetricsResponse`.
- C11: add `nurse-navigator`, `receiving-clinician` and `lab` to `DemoRoles.HumanRoles` (shared file; agree first).
- C15 (decision 13): `POST /cases/{id}/escalations/{escalationId}/acknowledgement` (role from the demo headers) and the acknowledgement as a timeline entry and on `CaseDetailResponse`. The endpoint saves an `EscalationAcknowledged` internal event through `ICaseRepository.SaveAsync`, like `DeadlineElapsed`. Only the role the escalation went to may acknowledge (the Domain enforces it); acknowledging never clears an exception, changes the owner or stops the clock.
- C19: `patientReference` filter on `GET /cases`, allowed for `clinician` and `receiving-clinician` only, refused for other roles, audited as a read under C4.
- C20: `PatientDisplayName` and `PatientSecondaryIdentifier` on `CaseSummaryResponse`, `CaseDetailResponse` and `ExceptionItemResponse` for staff roles only; null for `coordinator`, whose screens show no names.
- Decision 19: `DisplayLabel` on `CaseSummaryResponse` and `CaseDetailResponse`, from `CaseDisplayLabels.From`.

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

- C6: manual actions `POST /actions/consultation-request`, `/actions/consultation-answer`, `/actions/referral`, `/actions/appointment`, wired to the existing builders, with forms on the clinician actions page (screens 5 and 6). Kept (decision 16), plus a headless mode (`Scenarios:Headless` or equivalent) in which a scenario sends every hospital-system event and the ClientApp is not needed; the demo runs headless and shows only CareLoop's screens and the patient's phone.
- C20: the `DiagnosticReport` the simulator sends references or contains a synthetic FHIR `Patient` with a display name and an MRN, for WS3 to map.
- C21: outcome resources carry an evidence kind from the pathway's vocabulary (agree the code with WS3).
- C11: let `ActingStaff` take the roles `nurse-navigator`, `receiving-clinician` and `lab`, and offer them on the clinician actions page.
- Decision 4: AI summaries are approved in the CareLoop web app. The HIS simulator only receives the approved `DocumentReference`; no CareLoop endpoint other than `/fhir` is called.
- C13 (decision 7): a FHIR `Consent` builder (grant and withdrawal, with a synthetic caregiver contact point) and a matching `HisEventKind`, so a scenario can send `ConsentRecorded` (agree the resource shape with WS3, and the step action with WS10). If the "Caregiver appointment message" proposal is adopted, `AppointmentBuilder` also sends the place.
- Contract points waiting on WS7 (see the table below): `PUT` route shape, external system identifier (`his-simulator`), urgency on the review `Task`, the simulated marker, and staff notification transport.

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
- C10 (decision 3): a confidence rating action (1 to 5) on the inbox and a sixth `Communication` builder with category `confidence-rating`. A rating is not an acknowledgement.
- C13 (decision 7): an `Email` value in `InboxChannel` so caregiver messages are shown, not sent. How the simulator recognises a caregiver message is part of "Simulator provider wire format" (WS4).
- Decision 2: "I will come" sends an `acknowledgement` `Communication` only. There is no appointment-confirmation reply.
- Contract points waiting on WS8 (see the table below): `PUT` route shape, `Communication.category` codes, simulator provider wire format, and the simulated marker.

## WS9 AI handoff summary

**Owns**

- `src/api/CareLoop.Api/Features/Summaries/**`
- `src/api/CareLoop.Api/Contracts/Summaries/**`
- `src/api/CareLoop.Api/Infrastructure/Ai/**`
- `src/api/CareLoop.Api/Infrastructure/Persistence/Summaries/**`, `Infrastructure/Persistence/Configurations/HandoffSummaryRecordConfiguration.cs`
- `tests/api/CareLoop.Tests/Api/HandoffSummaryTests.cs`
- `src/ai_workflow/**` (decision 8: the Python service calls Foundry, so one workstream owns the whole summary path)

**Stubs to implement**

- `Features/Summaries/DraftHandoffSummary.cs`, `ApproveSummary.cs`, `RejectSummary.cs`
- `Features/Summaries/HandoffFactCollector.cs`: the single data boundary. Everything a model may write about passes through it
- `Features/Summaries/HandoffSummaryDrafter.cs`: the single draft path, shared by both triggers, validating every cited fact before anything is stored
- `Features/Summaries/AutoDraftOnHandoff.cs`: drafts when a handoff is proposed, behind `Ai:AutoDraftOnHandoff`, off by default
- `Infrastructure/Ai/AiWorkflowProvider.cs`: calls the Python service (the default route once AI is on)
- `Infrastructure/Ai/FoundryDirectProvider.cs`: calls a Foundry deployment from the API, the second route
- `POST /summaries/handoff` in `src/ai_workflow/`: owns the prompt, the response schema and the Foundry call. Specified in [`api-ai-workflow-handoff.md`](api-ai-workflow-handoff.md); not built
- `NoOpAiProvider` already works and stays the default, so everything runs with AI off

Implementing either provider needs a dependency that is not yet agreed: `Azure.Identity` for the API, `azure-identity` plus a Foundry SDK for the Python service. `AGENTS.md` says to ask first. Neither route has an API key: both authenticate with Entra ID.

The `HandoffSummaries` table needs a migration for the provenance columns (suggested name `HandoffSummaryProvenance`). Migrations are WS2's folder; agree who writes it.

Since decision 15 (C20), the case holds `PatientDisplayName` and `PatientSecondaryIdentifier`. `HandoffFactCollector` must never turn them into facts: decision 9 keeps names and identifiers out of every prompt.

Two contract points are still open for WS9 (see the table below): summary approver roles, and the removed `Ai:AzureOpenAi` section and API key setting that `appsettings.json` and `docker-compose.yml` still carry.

**Scope boundary:** the backend owns workflow state, ownership, deadlines, escalations, and metric calculation. The AI provider produces source-grounded, human-reviewed drafts only; it does not coordinate stages, calculate metrics, mutate cases, or send messages. [`api-ai-workflow-handoff.md`](api-ai-workflow-handoff.md) is the contract: what AI may do, what it is sent, how a draft is validated and what happens when it fails. [`ai-spec.md`](ai-spec.md) is the design proposal behind it, superseded by decisions 8 to 11 where the two differ; its patient-facing drafts, Arabic translation and periodic insight narrative are deferred.

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

**Design gaps**

- C8: scenarios for call-me then call-back, understand after a reminder, consultation rejected, missed appointment then rebook, closure `Declined`, and closure `Untraceable` with documented attempts. Grown (decision 16): the demo scenario also sends the hospital-system events of design screens 15, 16 and 19 to 21, in order: `ConsentRecorded/Granted` when the case opens, the assessment `AppointmentUpdated/Attended`, the lab `ReferralUpdated/Sent` and `/Accepted`, and `OutcomeRecorded` with kinds `diagnosis`, `treatment-plan` and `treatment-started` (C21), with an escalation on the late referral for screen 17 (C15).
- Decision 19: journey tests assert the display label at each step of the happy path.
- C21: `happy-path.json` still sends `PLACEHOLDER-EVIDENCE-KIND`, which the API now refuses; change every scenario outcome to a kind the pathway declares.
- C5: give `SimulationArmResponse` the same measures as `MetricsResponse` so the escalation-off comparison is like for like.
- C10: a `rate-confidence` patient behaviour in `scenario.schema.json` (agree with WS7 and WS8).
- C13: a scenario with a consented caregiver and one where consent is withdrawn. The schema's step `action` list has no consent action yet: add one (for example `consent`, with grant or withdraw) for the HIS simulator to send, agreed with WS7. The caregiver's appointment message also waits on the "Caregiver appointment message" contract point.
- Decision 2: patient behaviours stay `acknowledge`, `understand` and `call-me` (plus `rate-confidence`). There is no appointment-confirmation behaviour.

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
| `PUT` route shape | WS3, WS7, WS8 | The API maps `PUT /fhir/{resourceType}/{id}` (standard FHIR update). `AGENTS.md` writes `PUT /fhir/{resourceType}`. WS3 implemented `PUT /fhir/{resourceType}/{id}`; the URL id must equal the body id or the request is refused with 400. CareLoop's own outbound sends use the same shape (`PUT {baseUrl}/fhir/{type}/{id}`). WS7 and WS8 to confirm; `AGENTS.md` (shared) still to be aligned. |
| `Communication.category` codes | WS4, WS8 | Both sides use system `https://careloop.invalid/fhir/CodeSystem/communication-category`. WS8 proposes codes `delivery-status`, `acknowledgement`, `teach-back`, `call-me`, plus `confidence-rating` (decision 3, C10); `StatusMap` has all five. WS8 to confirm. |
| External system identifiers | WS2, WS3, WS7 | `ExternalSystems:Systems` is keyed by system identifier, and the key must equal the source system identifier that system sends. The development settings use `his-simulator`; the HIS simulator does not yet send an agreed identifier. WS3 proposal (implemented): the sender puts its identifier in `meta.source` on every resource, and a resource without it is refused with 400. The case keeps that value as its source system, and replies are routed to it. WS7 to confirm and send `his-simulator`. |
| Scenario patient behaviour | WS7, WS8, WS10 | A scenario's steps are hospital-system events only. How a patient reacts is `patients[].behaviour` (`delivery`, `response`, `respondAfter`, `respondToNotification`), applied by the patient-channel simulator when a notification arrives. Cohort runs need a way to give each generated patient their scenario's behaviour. |
| Time in a live run | WS1, WS5, WS7, WS8, WS10 | CareLoop runs on real time. A live run uses the demo pathway folder (`Pathways:Directory`), whose deadlines are divided by 1800, and both simulators pace scenarios with `Scenarios:RealSecondsPerScenarioHour` = 2. The three figures must stay equal. End-to-end tests should assert outcomes, not exact times, and leave headroom for slow machines. |
| Simulator provider wire format | WS4, WS8 | Private to the simulator provider: a FHIR `CommunicationRequest` to the patient-channel simulator, and a FHIR `Communication` back to `POST /notifications/inbound/simulator`. Agree the `Communication.category` codes and how the notification attempt identifier is carried. |
| Patient replies beyond acknowledgement | WS1, WS4, WS8 | Settled (decisions 2 and 3): the replies are acknowledgement, teach-back, call-me and confidence rating. Accepting an appointment is an acknowledgement; a patient reply never changes appointment status. |
| Simulated marker on FHIR resources | WS7, WS8 | The simulators mark the resources they send (the API test samples use `meta.tag` in system `.../data-origin` with code `simulated`; WS8 planned `meta.security` `HTEST`; pick one). CareLoop neither requires nor emits a marker. |
| Staff notification transport | WS4, WS7 | The HIS simulator offers both `POST /fhir/Task` and a plain `POST /staff-inbox/notifications`. Decide which `SimulatorNotificationProvider` uses and drop the other. |
| Urgency on the review `Task` | WS3, WS7 | `AGENTS.md` says `Task.priority`. Placeholder urgency codes do not fit the FHIR priority value set; decide between priority and an extension. WS3 implemented both, extension first: `TaskMapper` reads the `valueCode` of `https://careloop.invalid/fhir/StructureDefinition/reviewed-urgency` and, when it is absent, falls back to `Task.priority`. The value is stored unchanged. WS7 to confirm and send the extension. |
| Nine event types | WS2, WS3 | Delivery reports and patient responses share one event, `NotificationUpdated`. Caregiver consent is its own event, `ConsentRecorded` (C13), so there are exactly nine source events and handlers. |
| Internal events | WS2, WS5, WS6, WS9 | `DeadlineElapsed`, `HandoffSummaryApproved` and `EscalationAcknowledged` are saved through `ICaseRepository.SaveAsync` directly, not through a handler. |
| Rejected events | WS1, WS2 | A refused event is saved with `Disposition = Rejected`, so the attempt is audited through the same write path. |
| Stage model | WS1, WS6 | Settled (decision 1): five stages in the Domain; the design's ten stages are display labels derived from stage plus track statuses, by the API since decision 19. |
| Patient appointment confirmation | WS1, WS4, WS8 | Settled (decision 2): dropped. "I will come" is an acknowledgement; a patient reply never changes appointment status. |
| Confidence rating | WS1, WS2, WS3, WS4, WS6, WS8, WS10 | Settled (decision 3): build it (C10). `NotificationUpdateKind.ConfidenceRated` with a 1 to 5 value; `Communication.category` `confidence-rating`. |
| Where the AI summary is approved | WS6, WS7, WS9 | Settled (decision 4): the CareLoop web app case screen drafts and approves; the HIS simulator receives the approved `DocumentReference` only. The case screen also rejects (decision 10). |
| Where the model is called | WS9 | Settled (decision 8): the Python AI workflow service calls Foundry and the API calls it; `FoundryDirect` is a second route from the API. Both sit behind `IAiProvider`, whose `NoOp` default keeps AI off. |
| Clinical text in a prompt | WS2, WS9 | Settled (decision 9): clinician-finalized clinical facts may be summarized, as typed facts with a source reference and review status. They are read for one draft and never written to the coordination store. Patient names, contact details, images and unfinalized or conflicting readings are never sent. |
| Draft shape and reviewer decision | WS6, WS9 | Settled (decision 10): sections cite the fact identifiers they rest on; the record keeps deployment, prompt and policy versions; `Rejected` is a stored status beside `Draft` and `Approved`. |
| When a draft is produced | WS9 | Settled (decision 11): on request, and when a handoff is proposed behind `Ai:AutoDraftOnHandoff`, off by default. |
| Summary approver roles | WS1, WS9 | Open. Proposed as `summaryApproverRoles` in the pathway definition, so the rule stays configuration like every other role rule, rather than a constant in `DemoRoles`. |
| AI settings in the shared config files | WS9, plus whoever owns the file | Open. `appsettings.json`, `.env.example` and `docker-compose.yml` still carry the removed `Ai:AzureOpenAi` section and an API key setting. |
| Roles | WS1, WS6, WS7 | Settled (decision 5): add `nurse-navigator`, `receiving-clinician` and `lab` (C11). `DemoRoles` is shared; WS6 edits it after announcing. |
| Consultation reply deadline | WS1, WS2 | Settled (decision 6): `answer-consultation` and `respond-to-referral` replace `respond-to-handoff`; both placeholders (C12). |
| Caregiver and consent | WS1, WS2, WS4, WS8 | Settled (decision 7): build it (C13). `RecipientKind.Caregiver`, a `Consent` record on the case, caregiver gets date, time and place only, shown in the patient-channel simulator's `Email` channel; real email stays out of scope. |
| Caregiver appointment message | WS1, WS2, WS3, WS4 | Open; blocks the WS4 part of C13. `Case.BeginAppointment` raises no domain event, so `NotificationRouter` is never told an appointment was booked. `AppointmentUpdatePayload` carries `ScheduledFor` but no place. Proposal: WS1 raises a domain event (for example `AppointmentScheduled`, carrying date, time and place) when the appointment track starts or is rebooked; WS2 adds the place to the payload; WS3 maps it from `Appointment` (for example the `Location` participant's display name). WS4 then routes it to the caregiver while consent is active. |
| Acknowledgement in escalation notices | WS1, WS4, WS5 | Open. WS5 is to include who acknowledged the previous level in each `EscalationRaised` notice (C15), but `EscalationRaised` has no field for it and the staff template has only `{{hisLink}}`. Proposal: WS1 adds an optional acknowledged-by role and time to `EscalationRaised`, WS5 fills them, and WS4 adds a placeholder to the staff template. |
| Escalation acknowledgement | WS1, WS2, WS5, WS6 | Settled (decision 13): C15. Only the role the escalation went to may acknowledge; it is audited and never clears an exception, changes the owner or stops the clock. |
| Patient identifiers | WS1, WS2, WS3, WS6, WS7 | Settled (decision 15): C20. The screening result carries the patient's display name and MRN; the case stores them; staff roles read them, `coordinator` does not. `AGENTS.md` changes (shared; agree first). |
| Display labels | WS1, WS6, WS10 | Settled (decision 19): the ten labels are computed by a Domain function and returned on reads and in metrics; the web app never recomputes them. |
| Evidence kinds | WS1, WS2, WS3, WS6, WS7, WS10 | Settled (decision 20): C21. The pathway declares the kinds (placeholders); intake rejects others; a `treatment-plan` outcome starts the `start-treatment` deadline. The PM confirms the names; WS3 and WS7 agree the FHIR code mapping. |
| Reply deadline placeholders | WS1 | Settled (decision 18): the pathway file follows screen 4 of the design. Clock time, not working hours; a working-hours calendar is future work. |
| HIS simulator screens | WS7, WS10 | Settled (decision 16): screens stay (C6) and a headless mode is added; the demo runs headless. |
