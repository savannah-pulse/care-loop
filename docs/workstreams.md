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
Every item, split by workstream with its status, is in the [design gap tracker](#design-gap-tracker) at the end of this
file; update its row in the same change that finishes the work.

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
| 4 | Outbox and notifications | WS1 (domain events), WS2 (outbox tables) | Done |
| 5 | Deadlines and escalation | WS1, WS2 | Open |
| 6 | Read endpoints and dashboard | WS2 (schema) | Open |
| 7 | HIS simulator | WS3 (FHIR contract and codes) | Open |
| 8 | Patient-channel simulator | WS3 (FHIR contract and codes), WS4 (notification delivery) | Done |
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
- C21: `EvidenceKindDefinition` list on `PathwayDefinition` (kind, `clinicallyApproved`, optional `nextAction`: `treatment-plan` starts `start-treatment`, `treatment-started` and `surveillance-started` return to `decide-closure`); `JsonPathwayProvider` fails on missing or duplicate kinds; `TransitionRules` accepts `OutcomeRecorded` in `ClosureReview` without leaving the stage. Done.
- Decision 19: a pure function that derives one of the ten display labels from stage plus track statuses, for WS6 to return on reads and in metrics. Done (`CaseDisplayLabels.From(case, pathway)`).
- C13 (caregiver appointment message): domain event `AppointmentScheduled` (`EventId`, `CaseId`, `OccurredAt`, `DateTimeOffset ScheduledFor`, `string? Location`), raised by `Case.ScheduleAppointment(sourceReference, scheduledFor, at, location)` on a first booking, a rebooking after a missed appointment, and a move to another time; a resend at the same time raises nothing. `Case.BeginAppointment` still raises nothing (it is used when the source first reports an appointment already attended or missed). The place travels on the event only and is not stored, so a change of place alone raises nothing. Done.
- C15 (acknowledgement in escalation notices): `EscalationRaised` carries `string? AcknowledgedByRole` and `DateTimeOffset? AcknowledgedAt`, filled by `Case.Escalate` for level 2 and above from the most recent open escalation one level below, when it was acknowledged. The worker (WS5) only calls `Escalate`. Done.
- C15 (escalation identifier): `Escalation.Id` (`Guid`, assigned by `Case.Escalate` when empty, unique on the case), `EscalationRaised.EscalationId`, and `Case.AcknowledgeEscalation(Guid escalationId, Actor, DateTimeOffset)`, which refuses an unknown, resolved or superseded escalation so a person is recorded as seeing only what they were shown. Done.

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
- C13: caregiver consent is its own event, `CaseEventType.ConsentRecorded`, carried by `ConsentPayload` (granted or withdrawn, caregiver contact point, consent reference) and applied by `ConsentHandler`; persisted on the case. Done.
- C15: persist the escalation acknowledgement (migration). Done; saved under the internal event `EscalationAcknowledged`.
- C20: `PatientDisplayName` and `PatientSecondaryIdentifier` on `ScreeningResultPayload`, copied to the case by `ScreeningResultHandler`; migration. Done.
- C21: `OutcomeHandler` rejects an `EvidenceKind` the pathway does not declare (saved with `Disposition = Rejected`). Done; the case keeps the latest `OutcomeEvidenceKind`.
- Migrations (14 November 2026): the migrations were squashed into a single `InitialCreate` holding the whole current schema, including the WS9 `HandoffSummaries` provenance columns and the outbox delivery lease (`OutboxDeliveries.LeaseOwner`, `LeasedUntil`, added for WS4 and regenerated into the same `InitialCreate`). Up and down were run against PostgreSQL 14 before the lease columns were added; the regenerated migration differs only by those two nullable columns. A database created from an earlier migration must be dropped and recreated (`docker compose down -v`); there is no upgrade path from the old history. Add later schema changes as new migrations after `InitialCreate`.
- C13: `AppointmentUpdatePayload.Location` (`string?`); `AppointmentUpdateHandler` books and rebooks through `Case.ScheduleAppointment` with that place. Done.
- C15: `Escalation.Id` persisted as a unique uuid column; the old int key stays as the primary key under the name `Sequence` so stored escalations keep their order. Done.
- C4 and C19 (read audit storage): table `CaseReads`, entity `Infrastructure/Persistence/Audit/CaseReadRecord.cs` (`CaseId?`, actor reference, role and facility, `View`, `Query`, `OutsideTeam`, `Reason`, `ReadAt`), exposed as `CareLoopDbContext.CaseReads`. A read changes no case state, so it is not an `AuditEvent` and does not go through `SaveAsync`; WS6 appends a record per read. Done.

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
- C13: `AppointmentMapper` reads the place into `AppointmentUpdatePayload.Location` from the participant whose actor is a `Location` (`Location/...` reference, actor `type` `Location`, or a contained `Location`): `actor.display`, else the contained `Location.name`. `FhirPublisher` publishes nothing for `AppointmentScheduled`. Done; `Samples/Appointment.json` carries a synthetic `Location` participant.
- Review follow-up (done): a `meta.source` not configured under `ExternalSystems:Systems` is refused with 403; attendance and a missed appointment are dated at the appointment (`end`, else `start`), a booking when it was made; a caregiver's contact point uses `telecom` system `other`, like the patient's; an `active` `Consent` with a `deny` provision is a withdrawal, and a staff `performer` is recorded as its actor; the approved summary's `DocumentReference` is sent on `HandoffSummaryApproved`, not on `HandoffProposed`. From the PR review: an appointment's time is `meta.lastUpdated` first; a closure's `Provenance` counts only when it targets that case `Task`; a handoff rejection is published as the case `Task` with status `rejected` and the reason, unless a newer handoff is pending. `AGENTS.md` (shared) now shows the `PUT` route, the `Consent` and `Patient` mappings and these rules.

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

- C10 (decision 3): in `SimulatorInboundNotificationHandler`, map `Communication.category` `confidence-rating` to `NotificationUpdateKind.ConfidenceRated` and the 1 to 5 value into `NotificationUpdatePayload.Confidence`, by calling WS3's `StatusMap.ToNotificationUpdateKind` and `StatusMap.ToConfidence`. A rating is a measure, never an acknowledgement. Done.
- C13 (decision 7): `RecipientResolver` sends a caregiver with active consent the appointment date, time and place only, never the result. Add a caregiver template under `Templates/en/`, with a standard and an SMS-safe variant. Record each send as a `NotificationAttempt`. Check `Consent.IsActive` when the message is sent, not only when the event was raised, because the outbox may deliver after a withdrawal; a withdrawal stops every later send. `SimulatorNotificationProvider` marks the `CommunicationRequest` as a caregiver message on the `Email` channel, so the patient-channel simulator files it in its Email inbox (WS8). `EmailNotificationProvider` stays a stub. `Consent`, `RecipientKind.Caregiver`, `ConsentHandler` and `ConsentMapper` are done. Unblocked: route the new `AppointmentScheduled` domain event (`ScheduledFor`, `Location`, which may be null; WS1) to the caregiver, and to nobody else, while `Case.CaregiverConsent.IsActive`. The template shows date, time and place only; when `Location` is null it says the place will be confirmed rather than inventing one. Done when: `NotificationRouter` handles `AppointmentScheduled`; a test covers send with active consent, no send with none or withdrawn consent (including a withdrawal saved after the event was raised), the SMS-safe variant, and a `NotificationAttempt` per send. Done: templates `caregiver-appointment` and `caregiver-appointment-place-tbc`, whose SMS-safe variants leave out the place (a clinic name can say what the appointment is for). The caregiver's language is the patient's, falling back to English. Caregiver attempts are not recorded on the case's notification track, which follows the patient; the caregiver `CommunicationRequest` id is prefixed `caregiver-`, and reports based on it are ignored, so a caregiver's delivery or acknowledgement never counts as the patient's. A booking without a time raises no `AppointmentScheduled` (WS2), so nobody is told a time the source never gave.
- C15 (decision 13): each `EscalationRaised` staff notice says who acknowledged the previous level, if anyone (WS5 sends the event). `EscalationRaised.AcknowledgedByRole` and `AcknowledgedAt` now exist (WS1, filled by `Case.Escalate`). Add a placeholder to the staff template that renders, for example, "Level 1 was acknowledged by facility-lead at 09:40", and nothing when both are null. Staff notices still carry no patient detail. Done when: a template test covers both cases. Done: `{{previousAcknowledgement}}` in both staff templates, filled by `NotificationRouter.PreviousAcknowledgementFor` (for example "Level 1 was acknowledged by facility-lead at 09:40 UTC on 2026-11-14.").
- Decision 2: a patient reply never changes appointment status. "I will come" arrives as `acknowledgement` and is translated to `Acknowledged` only.
- Contract points settled by WS4: "Staff notification transport" and "Simulator provider wire format" (see the table below). Language: a template missing in the recipient's language falls back to English (`TemplateRenderer.FallbackLanguage`).
- Delivery guarantees: several API instances can run the dispatcher; each delivery is claimed with one conditional update (`OutboxDelivery.LeaseOwner`, `LeasedUntil`, `Outbox:LeaseDuration`) and only the claim owner records the outcome. A staff or caregiver send a provider refused makes the router throw, so the outbox retries it (then parks it); a patient refusal is recorded on the case only when no channel accepted.
- Known limits: `NotificationSendLog` (which remembers accepted attempts so a retry resends only failed channels) is in memory, so a restart, or a claim taken over by another instance, can send an accepted message again; attempt identifiers are deterministic, so the receiver can drop the repeat. A durable record needs a WS2 table. The caregiver message shows the time in the offset the hospital system sent; showing facility local time needs a configured time zone.

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

- C15: an acknowledged escalation is still overdue. The sweep keeps escalating up the ladder on the pathway's `afterOverdue`; acknowledgement never stops the clock. Raise each step with `Case.Escalate`, which fills `EscalationRaised.AcknowledgedByRole` and `AcknowledgedAt` from the previous level and assigns `Escalation.Id`; do not build `EscalationRaised` yourself. Done when: a `DeadlineWorkerTests` case acknowledges level 1, sweeps past level 2's `afterOverdue`, and asserts level 2 is raised carrying the acknowledgement.
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

- C1: `GET /programmes` and `GET /programmes/{pathwayId}` with a `ProgrammeResponse` contract (new `Features/Programmes/`, `Contracts/Programmes/`). Source: `IPathwayProvider.GetAll()` and `Get(id)`; the C2 fields (`citation`, `changeLog`, `ownerRole`, `clinicallyApproved`, evidence kinds) are already on `PathwayDefinition`. An unknown id is 404.
- C3: `SourceSystem` on `TimelineEntryResponse`. The timeline reads `AuditEvents`, which do not hold the source system: join `AuditEvent.SourceEventId` to `SourceEvent.SourceSystem`; internal events (`DeadlineElapsed`, `EscalationAcknowledged`, `HandoffSummaryApproved`) show CareLoop as their source.
- C4: `facilityId` filter on `GET /cases`, refusal of case reads outside the caller's facility, a read audit per read, and `X-Demo-Facility` in `DemoRoles` (shared file; agree first). Added (decision 12): a read outside the caller's team requires a `reason` query parameter, stored in the same audit entry as the view. Storage exists: append a `CaseReadRecord` to `CareLoopDbContext.CaseReads` (WS2) for `GET /cases/{id}`, `/timeline` and filtered lists, with `OutsideTeam` and `Reason`. Done when: tests cover an in-team read, a refused out-of-team read without a reason, an allowed one with a reason, and one record per read.
- C5: the screen 3 measures on `MetricsResponse`, each with its denominator (with WS10 for `SimulationArmResponse`): time-to-step medians, understanding rate, closures by reason, cases reaching each display label (decision 19), referral accepted and attended, appointments attended, treatment started by its deadline and time to care started (both from the C21 evidence kinds: denominator is cases with a `treatment-plan` outcome, numerator a `treatment-started` outcome before the `start-treatment` due time).
- C9: build screens 1 to 4, 17 and 18 of the design in `src/web/` (decision 17), against the endpoints above. Read-only except: the handoff summary screen drafts, edits on approval and rejects (decisions 4 and 10); the escalation screen acknowledges (C15); the other-cases card for clinician roles (C19). Show the display label the API returns (decision 19); do not recompute it.
- C10: `confidenceRate` on `MetricsResponse`.
- C11: add `nurse-navigator`, `receiving-clinician` and `lab` to `DemoRoles.HumanRoles` (shared file; agree first).
- C15 (decision 13): `POST /cases/{id}/escalations/{escalationId}/acknowledgement` (role from the demo headers) and the acknowledgement as a timeline entry and on `CaseDetailResponse`. `{escalationId}` is `Escalation.Id`; call `Case.AcknowledgeEscalation(escalationId, actor, at)` and map its refusals (wrong role, superseded, already acknowledged) to 409 or 403. The endpoint saves an `EscalationAcknowledged` internal event through `ICaseRepository.SaveAsync`, like `DeadlineElapsed`. `CaseDetailResponse` lists escalations with id, level, escalated-to role, raised at, acknowledged-by role and time, and resolved at. Only the role the escalation went to may acknowledge (the Domain enforces it); acknowledging never clears an exception, changes the owner or stops the clock.
- C19: `patientReference` filter on `GET /cases`, allowed for `clinician` and `receiving-clinician` only, refused for other roles, audited as a read under C4 (`View` `case-list`, `Query` naming the filter).
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
- C13 (decision 7): a FHIR `Consent` builder (grant and withdrawal, with a synthetic caregiver contact point) and a matching `HisEventKind`, so a scenario can send `ConsentRecorded` (agree the resource shape with WS3, and the step action with WS10). `AppointmentBuilder` also sends the place as a participant whose `actor` is a `Location` reference with a synthetic `display` (for example `SYN Breast Imaging Clinic`); WS3's mapper reads it (settled; see `tests/api/CareLoop.Tests/Api/Fhir/Samples/Appointment.json`).
- Contract points waiting on WS7 (see the table below): `PUT` route shape, external system identifier (`his-simulator`), urgency on the review `Task`, the simulated marker, staff notification transport, and the contact point channel (`telecom` system `other` for the patient and the caregiver).

## WS8 Patient-channel simulator

**Owns**

- `src/simulators/patient-channel/**`, `tests/simulators/patient-channel/**`

**Stubs to implement**

- `Inbound/`: `CommunicationRequestReceiver`, `CommunicationRequestMapper`, `DeliverySimulator`, `AutomaticResponder`, and the inbox, patient behaviour, message log and case status endpoints
- `Outbound/`: the six `Communication` builders (the sixth is the confidence rating, C10), `CareLoopFhirClient`, `PatientResponseSender`, `CaseStatusReader`
- `Scenarios/`: `ScenarioLoader`, `ScenarioRunner`, scenario endpoints
- `State/SimulatorStore.cs`
- `ClientApp/src/features/*`: inbox, case status, patient behaviours, scenario runner, message log

**Design gaps**

- C7 (Done): on the `Sms` channel accept an inbound reply of `1` as acknowledge and `2` as call-me (screen 9). Built as `POST /api/inbox/{messageId}/reply`; any other text is refused and nothing is sent. CareLoop sends no channel, so a patient message lands in the `Sms` inbox when its inbox address starts with `SYN-SMS-` (`SYN-PORTAL-` for the portal; anything else goes to `PatientChannel:DefaultChannel`, `App`). The SMS-safe template is not shown there, because the simulator provider allows sensitive content and so always receives the standard text.
- C10 (Done, decision 3): a confidence rating action (1 to 5) on the inbox and a sixth `Communication` builder with category `confidence-rating`. A rating is not an acknowledgement. Built as `POST /api/inbox/{messageId}/rate-confidence`; a patient behaviour can also rate (`rate-confidence`, or a `confidence` beside another response).
- C13 (Done, decision 7): an `Email` value in `InboxChannel` so caregiver messages are shown, not sent. A caregiver message is recognised by its `caregiver` recipient kind; it gets a delivery report, is never answered and cannot be acted on.
- Decision 2 (Done): "I will come" sends an `acknowledgement` `Communication` only. There is no appointment-confirmation reply.
- Contract points (see the table below): WS8 has confirmed the `Communication.category` codes, the simulator provider wire format and the simulated marker, and sends no `PUT`.

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

The `HandoffSummaries` table, provenance columns included, is in the `InitialCreate` migration. Further schema changes need a new migration in WS2's folder; agree who writes it.

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
- C10 (Done, edited by WS8): a `rate-confidence` patient behaviour in `scenario.schema.json`, plus an optional `confidence` (1 to 5) beside another response. No scenario file uses it yet.
- C13: a scenario with a consented caregiver and one where consent is withdrawn. The schema's step `action` list has no consent action yet: add one (for example `consent`, with grant or withdraw) for the HIS simulator to send, agreed with WS7. The consented-caregiver scenario books an appointment with a place and expects the caregiver's Email message (date, time and place only); the withdrawn one books after withdrawal and expects no caregiver message.
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
| `PUT` route shape | WS3, WS7, WS8 | The API maps `PUT /fhir/{resourceType}/{id}` (standard FHIR update). `AGENTS.md` writes `PUT /fhir/{resourceType}`. WS3 implemented `PUT /fhir/{resourceType}/{id}`; the URL id must equal the body id or the request is refused with 400. CareLoop's own outbound sends use the same shape (`PUT {baseUrl}/fhir/{type}/{id}`). WS7 to confirm. WS8 sends no `PUT`: it posts `Communication`s to the notification callback and reads `GET /fhir/Task/{id}`. `AGENTS.md` is aligned. |
| `Communication.category` codes | WS4, WS8 | Both sides use system `https://careloop.invalid/fhir/CodeSystem/communication-category`. Settled: codes `delivery-status`, `acknowledgement`, `teach-back`, `call-me`, plus `confidence-rating` (decision 3, C10, value in the first `payload.contentString`); `StatusMap` has all five and WS8 sends them. |
| External system identifiers | WS2, WS3, WS7 | `ExternalSystems:Systems` is keyed by system identifier, and the key must equal the source system identifier that system sends. The development settings use `his-simulator`; the HIS simulator does not yet send an agreed identifier. WS3 proposal (implemented): the sender puts its identifier in `meta.source` on every resource, and a resource without it is refused with 400; one whose value is not configured under `ExternalSystems:Systems` is refused with 403, because no reply to it could be delivered. The case keeps that value as its source system, and replies are routed to it. WS7 to confirm and send `his-simulator`. |
| Scenario patient behaviour | WS7, WS8, WS10 | A scenario's steps are hospital-system events only. How a patient reacts is `patients[].behaviour` (`delivery`, `response`, `respondAfter`, `respondToNotification`, `confidence`), applied by the patient-channel simulator when a notification arrives. `response` may be `rate-confidence` (C10), which needs a `confidence`; `respondToNotification` counts the notifications received since the behaviour was put in force. Cohort runs need a way to give each generated patient their scenario's behaviour. |
| Time in a live run | WS1, WS5, WS7, WS8, WS10 | CareLoop runs on real time. A live run uses the demo pathway folder (`Pathways:Directory`), whose deadlines are divided by 1800, and both simulators pace scenarios with `Scenarios:RealSecondsPerScenarioHour` = 2. The three figures must stay equal. End-to-end tests should assert outcomes, not exact times, and leave headroom for slow machines. |
| Simulator provider wire format | WS4, WS8 | Settled by WS4 (`SimulatorNotificationProvider`): `POST {PatientChannelBaseUrl}/fhir/CommunicationRequest` with the attempt identifier as the `https://careloop.invalid/fhir/sid/notification-attempt` identifier and as the id (prefixed `caregiver-` for a caregiver message), category `patient` or `caregiver` in `https://careloop.invalid/fhir/CodeSystem/recipient-kind` (a caregiver message belongs in the Email inbox), `recipient` naming the inbox, `about` the case `Task`, `payload` the text and `language`. The simulator answers with a `Communication` to `POST /notifications/inbound/simulator` whose `basedOn` is that request and `about` the case `Task`; reports based on a caregiver request are ignored. Each post must carry `X-CareLoop-Signature: sha256=` plus the lower-case hex HMAC-SHA256 of the raw UTF-8 body, keyed with the secret shared through `SIMULATOR_INBOUND_SECRET` (`Notifications:Simulator:InboundSecret` in the API); anything unsigned or wrongly signed gets 401. WS8 signs in `CareLoopFhirClient.SendCommunicationAsync` (its setting is `CareLoop:InboundSecret`; with no secret it sends nothing). Each `Communication` has the id `{request id}-{kind}` and a `meta.versionId`, which a resend repeats. |
| Patient replies beyond acknowledgement | WS1, WS4, WS8 | Settled (decisions 2 and 3): the replies are acknowledgement, teach-back, call-me and confidence rating. Accepting an appointment is an acknowledgement; a patient reply never changes appointment status. |
| Simulated marker on FHIR resources | WS7, WS8 | The simulators mark the resources they send with `meta.tag` in system `https://careloop.invalid/fhir/CodeSystem/data-origin`, code `simulated`, as the API test samples do. WS8 sends it; WS7 to follow. CareLoop neither requires nor emits a marker. |
| Staff notification transport | WS4, WS7 | Settled by WS4: a FHIR `Task` with code `staff-notice`, sent with `PUT {StaffInboxBaseUrl}/fhir/Task/staff-notice-{attempt}` so a replay overwrites; `owner` names the staff inbox, `focus` the case `Task`, `description` the text. WS7 to drop `POST /staff-inbox/notifications` and accept the `PUT`. |
| Urgency on the review `Task` | WS3, WS7 | `AGENTS.md` says `Task.priority`. Placeholder urgency codes do not fit the FHIR priority value set; decide between priority and an extension. WS3 implemented both, extension first: `TaskMapper` reads the `valueCode` of `https://careloop.invalid/fhir/StructureDefinition/reviewed-urgency` and, when it is absent, falls back to `Task.priority`. The value is stored unchanged. WS7 to confirm and send the extension. |
| Nine event types | WS2, WS3 | Delivery reports and patient responses share one event, `NotificationUpdated`. Caregiver consent is its own event, `ConsentRecorded` (C13), so there are exactly nine source events and handlers. |
| Internal events | WS2, WS5, WS6, WS9 | `DeadlineElapsed`, `HandoffSummaryApproved` and `EscalationAcknowledged` are saved through `ICaseRepository.SaveAsync` directly, not through a handler. |
| Contact point channel | WS3, WS4, WS7, WS8 | Settled with this review: a `ContactPoint.system` of `other` maps to the simulator channel, for the patient's `Patient.telecom` and the caregiver's `RelatedPerson.telecom` alike; `sms` and `email` are for the real providers and stay unreachable while only the simulator provider is enabled. The simulator provider tells the patient-channel simulator the recipient kind, so a caregiver message lands in its Email inbox (WS4, WS8). WS7 to send `other`. WS8 proposal for WS7 and WS10: a patient contact address starting `SYN-SMS-` is shown in the simulator's `Sms` inbox and `SYN-PORTAL-` in its portal inbox; any other address goes to the health app. |
| Publishing the approved summary | WS1, WS3, WS9 | Settled with this review: a new domain event, `HandoffSummaryApproved` (summary id and approver), is raised by `ApproveSummary` (WS9) and published by `FhirPublisher` as the `DocumentReference` for that summary only. `HandoffProposed` sends the case `Task` alone, so a summary approved after the proposal is still sent and an older one never is. |
| Repeated requests | WS2, WS3 | Settled with this review: the HIS sends a consultation or referral as a `ServiceRequest` plus a `Task`, and resends new versions. A request repeated while the consultation is already requested, or the referral already sent, is recorded but leaves the deadline alone; a request after a rejection restarts it. |
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
| Caregiver appointment message | WS1, WS2, WS3, WS4, WS7 | Settled and built (WS1 to WS3): domain event `AppointmentScheduled` (`ScheduledFor`, `Location`) raised by `Case.ScheduleAppointment` on booking, rebooking and a change of time; `AppointmentUpdatePayload.Location`; `AppointmentMapper` reads the `Location` participant's `actor.display`; `FhirPublisher` ignores the event. WS4 routes it to the caregiver while consent is active, and `AGENTS.md`'s "Events and notifications" table has its row (no FHIR message; caregiver with active consent). A booking without a time raises nothing. Remaining: WS7 sends the `Location` participant. |
| Acknowledgement in escalation notices | WS1, WS4, WS5 | Settled and built (WS1, WS4): `EscalationRaised.AcknowledgedByRole` and `AcknowledgedAt`, filled by `Case.Escalate` from the previous level, so WS5 only calls `Escalate`; WS4's staff templates show them through `{{previousAcknowledgement}}`. |
| Escalation identifier | WS1, WS2, WS6 | Settled and built (WS1, WS2): `Escalation.Id` (uuid), carried on `EscalationRaised.EscalationId`, persisted unique; `Case.AcknowledgeEscalation(escalationId, ...)` refuses a superseded escalation. WS6's acknowledgement endpoint takes it as `{escalationId}`. |
| Read audit | WS2, WS6 | Settled and built (WS2): staff reads are recorded in `CaseReads` (`CaseReadRecord`), not in `AuditEvents`, because a read changes no case state and has no source event. WS6 writes one per read (C4, C19). |
| Escalation acknowledgement | WS1, WS2, WS5, WS6 | Settled (decision 13): C15. Only the role the escalation went to may acknowledge; it is audited and never clears an exception, changes the owner or stops the clock. |
| Patient identifiers | WS1, WS2, WS3, WS6, WS7 | Settled (decision 15): C20. The screening result carries the patient's display name and MRN; the case stores them; staff roles read them, `coordinator` does not. `AGENTS.md` changes (shared; agree first). |
| Display labels | WS1, WS6, WS10 | Settled (decision 19): the ten labels are computed by a Domain function and returned on reads and in metrics; the web app never recomputes them. |
| Evidence kinds | WS1, WS2, WS3, WS6, WS7, WS10 | Settled (decision 20): C21. The pathway declares the kinds (placeholders); intake rejects others; a `treatment-plan` outcome starts the `start-treatment` deadline. The PM confirms the names; WS3 and WS7 agree the FHIR code mapping. |
| Reply deadline placeholders | WS1 | Settled (decision 18): the pathway file follows screen 4 of the design. Clock time, not working hours; a working-hours calendar is future work. |
| HIS simulator screens | WS7, WS10 | Settled (decision 16): screens stay (C6) and a headless mode is added; the demo runs headless. |

## Design gap tracker

One row per item and workstream, so no part is lost between owners. **Open** means not started or not merged;
**Done** means built, tested and merged on `main` (or on this branch's pull request). Change a row only in the change
that finishes it. Items come from [`ui-design-gap-analysis.md`](ui-design-gap-analysis.md); C14 and C16 to C18 were
withdrawn.

| Item | WS | Part | Status |
|---|---|---|---|
| C1 Programmes read model | WS6 | `GET /programmes`, `GET /programmes/{pathwayId}`, `ProgrammeResponse` | Open |
| C1 | WS6 | Web: programmes screen (screen 4, part of C9) | Open |
| C2 Pathway citations and change log | WS1 | `citation`, `changeLog`, `ownerRole` in both pathway files | Done |
| C3 Source system on the timeline | WS6 | `TimelineEntryResponse.SourceSystem` via `SourceEvent` | Open |
| C4 Team-scoped, audited reads | WS2 | `CaseReads` table and `CaseReadRecord` | Done |
| C4 | WS6 | `facilityId` filter, out-of-team refusal, required `reason`, one `CaseReadRecord` per read | Open |
| C4 | WS6 (shared) | `X-Demo-Facility` in `DemoRoles` | Open |
| C5 Screen 3 measures | WS6 | `MetricsResponse` measures with denominators, incl. display-label counts and C21 treatment measures | Open |
| C5 | WS10 | `SimulationArmResponse` with the same measures | Open |
| C6 HIS clinician actions and headless mode | WS7 | `/actions/consultation-request`, `/consultation-answer`, `/referral`, `/appointment`, forms, headless mode | Open |
| C7 SMS reply codes | WS8 | `1` acknowledge, `2` call-me on the `Sms` inbox | Done |
| C8 Exception-path and demo scenarios | WS10 | Six exception scenarios with `expect` entries; demo scenario for screens 15, 16, 19 to 21 | Open |
| C8 | WS10 | Replace `PLACEHOLDER-EVIDENCE-KIND` in `happy-path.json` with a declared kind (C21) | Open |
| C9 Dashboard | WS6 | Screens 1 to 4, 17, 18; summary draft, edit-on-approve, reject; escalation acknowledge; other-cases card | Open |
| C10 Confidence rating | WS1, WS2, WS3 | Domain, payload, handler, `StatusMap` | Done |
| C10 | WS4 | `SimulatorInboundNotificationHandler` maps `confidence-rating` | Done |
| C10 | WS6 | `confidenceRate` on `MetricsResponse` | Open |
| C10 | WS8 | Inbox rating action and `confidence-rating` `Communication` builder | Done |
| C10 | WS10 | `rate-confidence` behaviour in `scenario.schema.json` (edited by WS8) | Done |
| C11 Roles | WS1 | Pathway `allowedRoles`, `authorizedRoles`, `OwnershipRules` | Done |
| C11 | WS6 (shared) | `nurse-navigator`, `receiving-clinician`, `lab` in `DemoRoles.HumanRoles` | Open |
| C11 | WS7 | `ActingStaff` roles and clinician actions page | Open |
| C12 Per-kind reply deadlines | WS1, WS2 | Pathway deadlines and next actions | Done |
| C12 | WS5 | Ordinary deadlines in `ComputeDueAt`, clock time | Open (with the WS5 stubs) |
| C13 Caregiver and consent | WS1, WS2, WS3 | `Consent`, `ConsentRecorded`, `ConsentMapper`; `AppointmentScheduled`, `Location` on payload and mapper | Done |
| C13 | WS4 | Route `AppointmentScheduled` to a consented caregiver; caregiver templates (standard and SMS-safe); consent checked at send | Done |
| C13 | WS7 | `Consent` builder and `HisEventKind`; `Location` participant on `AppointmentBuilder` | Open |
| C13 | WS8 | `Email` in `InboxChannel`; recognise caregiver messages | Done |
| C13 | WS10 | Consent `action` in the schema; consented and withdrawn scenarios | Open |
| C13 | Shared | `AGENTS.md` events table row for `AppointmentScheduled` | Done |
| C15 Escalation acknowledgement | WS1, WS2 | `AcknowledgeEscalation`, persistence, `Escalation.Id`, `EscalationRaised` acknowledgement fields | Done |
| C15 | WS4 | Staff template placeholder for the previous level's acknowledgement | Done |
| C15 | WS5 | Sweep keeps escalating after acknowledgement, through `Case.Escalate` | Open (with the WS5 stubs) |
| C15 | WS6 | `POST /cases/{id}/escalations/{escalationId}/acknowledgement`, timeline entry, escalations on `CaseDetailResponse` | Open |
| C19 Patient's other cases | WS6 | `patientReference` filter for clinician roles, audited in `CaseReads` | Open |
| C20 Patient identifiers | WS1, WS2, WS3 | Case fields, payload, `DiagnosticReportMapper` | Done |
| C20 | WS6 | Name and MRN on staff reads, null for `coordinator` | Open |
| C20 | WS7 | `DiagnosticReport` with a synthetic `Patient` | Open |
| C21 Evidence kinds | WS1, WS2, WS3 | Pathway vocabulary, intake rejection, mapper | Done |
| C21 | WS5 | `start-treatment` handled like any other deadline | Open (with the WS5 stubs) |
| C21 | WS7 | Outcome resources carry a declared kind | Open |
| Decision 19 Display labels | WS1 | `CaseDisplayLabels.From` | Done |
| Decision 19 | WS6 | `DisplayLabel` on case reads and in metrics | Open |
| Decision 19 | WS10 | Journey tests assert the label at each happy-path step | Open |
| Summary approver roles | WS1, WS9 | `summaryApproverRoles` in the pathway (open contract point) | Open (decision needed) |

