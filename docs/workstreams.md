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
| 5 | Deadlines and escalation | WS1, WS2 | Done |
| 6 | Read endpoints and dashboard | WS2 (schema) | Done |
| 7 | HIS simulator | WS3 (FHIR contract and codes) | Done |
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

**Implementation**

- Complete: urgency-specific deadline lookup with general fallback; clock-time due dates; warning and overdue
  boundary classification; per-deadline escalation ladder checks. WS2's temporary lookup now delegates to the Domain policy.
- The enabled worker persists warnings once per case/action/due time, including across restarts; flags overdue and
  unacknowledged cases; catches up due ladder steps through `Case.Escalate`; saves every change as `DeadlineElapsed`
  with audit and outbox deliveries in the single transaction. It never changes owner, stage, urgency or deadline.
- Checks use isolated scopes. Errors are logged and retried on the next sweep; optimistic concurrency rolls back
  stale checks. The polling interval is validated at startup. No schema or clinical configuration changes were needed.
- The sweep reads only each open case's id and due time and loads full cases only inside the largest pathway
  warning window. The "acknowledge notification" action key is a named constant, and a test fails if a shipped
  pathway does not define a deadline for it.
- Production keeps the 30-second sweep. The Development settings and Docker Compose set `DeadlineWorker:Interval`
  to one second, because the demo pathway's deadlines last seconds and a 30-second sweep skips warnings.
- All WS5 skipped tests are implemented. Tests cover boundary instants, configurable durations, repeat sweeps,
  restarts, new deadlines, late sweeps, acknowledgement propagation, audit/outbox persistence, cancellation,
  the background loop, error isolation and a concurrent case update.

**Design gaps**

- C15: an acknowledged escalation is still overdue. The sweep keeps escalating up the ladder on the pathway's `afterOverdue`; acknowledgement never stops the clock. Raise each step with `Case.Escalate`, which fills `EscalationRaised.AcknowledgedByRole` and `AcknowledgedAt` from the previous level and assigns `Escalation.Id`; do not build `EscalationRaised` yourself. Done: a worker test acknowledges level 1 and asserts level 2 is raised at its threshold carrying that acknowledgement.
- C21: `ComputeDueAt` handles the `start-treatment` deadline like any other next action; no special case. Done.
- C12 and decision 18: `answer-consultation` (1 day), `respond-to-referral` (4 hours) and `call-patient` (2 hours) are ordinary next-action deadlines. Durations are clock time: add no working-hours calendar, which stays future work. Done.

## WS6 Read endpoints and dashboard

**Owns**

- `src/api/CareLoop.Api/Features/Cases/**`, `Features/Exceptions/**`, `Features/Metrics/GetMetrics.cs`, `Features/Common/**`
- `src/api/CareLoop.Api/Contracts/Cases/**`, `Contracts/Exceptions/**`, `Contracts/Common/**`, `Contracts/Metrics/MetricsResponse.cs`
- `contracts/openapi/**` (generated by the API build; commit it when routes or contracts change)
- `tests/api/CareLoop.Tests/Api/ReadEndpointsTests.cs`
- `src/web/**` (except `src/web/tests/e2e/**` and `src/web/playwright.config.ts`; see `src/web/README.md`)

**Stubs to implement**

- `ListCases`, `GetCase`, `GetTimeline`, `GetExceptions`, `GetMetrics`
- `src/web/src/features/exceptions`, `cases`, `metrics`: replace each placeholder page
- Do not hand-edit `src/web/src/api/schema.d.ts`; run `npm run generate:api` in `src/web`

**Design gaps**

- C1: `GET /programmes` and `GET /programmes/{pathwayId}` with a `ProgrammeResponse` contract (new `Features/Programmes/`, `Contracts/Programmes/`). Source: `IPathwayProvider.GetAll()` and `Get(id)`; the C2 fields (`citation`, `changeLog`, `ownerRole`, `clinicallyApproved`, evidence kinds) are already on `PathwayDefinition`. An unknown id is 404.
- C3: `SourceSystem` on `TimelineEntryResponse`. The timeline reads `AuditEvents`, which do not hold the source system: join `AuditEvent.SourceEventId` to `SourceEvent.SourceSystem`; internal events (`DeadlineElapsed`, `EscalationAcknowledged`, `HandoffSummaryApproved`) show CareLoop as their source.
- C4: `facilityId` filter on `GET /cases`, refusal of case reads outside the caller's facility, a read audit per read, and `X-Demo-Facility` in `DemoRoles` (shared file; agree first). Added (decision 12): a read outside the caller's team requires a `reason` query parameter, stored in the same audit entry as the view. Storage exists: append a `CaseReadRecord` to `CareLoopDbContext.CaseReads` (WS2) for `GET /cases/{id}`, `/timeline` and filtered lists, with `OutsideTeam` and `Reason`. Done when: tests cover an in-team read, a refused out-of-team read without a reason, an allowed one with a reason, and one record per read.
- C5: the screen 3 measures on `MetricsResponse`, each with its denominator (with WS10 for `SimulationArmResponse`): time-to-step medians, understanding rate, closures by reason, cases reaching each display label (decision 19), referral accepted and attended, appointments attended, treatment started by its deadline and time to care started (both from the C21 evidence kinds: denominator is cases with a `treatment-plan` outcome, numerator a `treatment-started` outcome before the `start-treatment` due time).
- C9: build screens 1 to 4, 17 and 18 of the design in `src/web/` (decision 17), against the endpoints above. Read-only except: the handoff summary screen drafts, edits on approval and rejects (decisions 4 and 10); the escalation screen acknowledges (C15); the other-cases card for clinician roles (C19). Show the display label the API returns (decision 19); do not recompute it.
- C10: `confidenceRate` on `MetricsResponse`.
- C11: add `nurse-navigator`, `receiving-clinician` and `lab` to `DemoRoles.HumanRoles` (shared file; agree first).
- C15 (decision 13): `POST /cases/{id}/escalations/{escalationId}/acknowledgement` (role from the demo headers) and the acknowledgement as a timeline entry and on `CaseDetailResponse`. `{escalationId}` is `Escalation.Id`; call `Case.AcknowledgeEscalation(escalationId, actor, at)` and map its refusals (wrong role, superseded, already acknowledged) to 409 or 403. The endpoint saves an `EscalationAcknowledged` internal event through `ICaseRepository.SaveAsync`, like `DeadlineElapsed`. `CaseDetailResponse` lists escalations with id, level, escalated-to role, raised at, acknowledged-by role and time, and resolved at. Only the role the escalation went to may acknowledge (the Domain enforces it), and a non-coordinator must also act in the facility it was escalated to: the endpoint refuses another facility's lead with 403 even when a cross-facility reason is given. (The Domain does not repeat the facility check because coordinators act without a facility; moving it into `Case` needs a rule for them first.) Acknowledging never clears an exception, changes the owner or stops the clock.
- C19: `patientReference` filter on `GET /cases`, allowed for `clinician` and `receiving-clinician` only, refused for other roles, audited as a read under C4 (`View` `case-list`, `Query` naming the filter).
- C20: `PatientDisplayName` and `PatientSecondaryIdentifier` on `CaseSummaryResponse`, `CaseDetailResponse` and `ExceptionItemResponse` for staff roles only; null for `coordinator`, whose screens show no names.
- Decision 19: `DisplayLabel` on `CaseSummaryResponse` and `CaseDetailResponse`, from `CaseDisplayLabels.From`.

**Completion**

All WS6 portions of C1, C3, C4, C5, C9, C10, C11, C15, C19, C20 and decision 19 are **Done**.
Facility is the current team boundary; cross-facility reads require a recorded reason. Coordinators have global
aggregate measures but case reads only for open coordinator escalations, with names/MRNs redacted. This follows the
design's "Who sees what" table (a coordinator sees one case only when it is escalated to them) and is a requirement
of this workstream, not only a completion note; the review's request to confirm it is recorded as a decision for the
product owner.
Read lists are resilient to inconsistent data: an exception row with no deadline or next action is returned with
those fields null, and a metric leaves out (and logs) a case whose history cannot support it, instead of failing the
whole read. `GET /exceptions` accepts `skip` and `take` (default and maximum 200).
Known design question for WS1: once a level 2 escalation exists, level 1 can no longer be acknowledged (superseded).
Confidence is rating participation with the latest-rating average separately reported; all durations are clock time.
Historical metrics use applied snapshots and the shared Domain display-label rule.

Validation: 560 API tests (34 skipped, all WS9/WS10) and 59 dashboard tests (jsdom interaction tests, run by CI) passed; dashboard lint/type-check/build passed.
Synthetic browser interception verified acknowledgement, role redaction, mobile layout, programmes/impact and exact
edited-text approval. WS9 summary persistence/provider endpoints and WS10 real end-to-end/comparison remain Open;
the completed summary UI explicitly shows backend 501/unavailable errors. Eight moderate development-only parser/
documentation-tool dependency findings remain; production dependency audit has zero findings.

## WS7 HIS simulator

**Owns**

- `src/simulators/his/**`, `tests/simulators/his/**`

**Implemented**

- Independent FHIR builders and client, dependency-ordered sends, replay-stable source versions, refusal logging and actual CareLoop case-Task correlation. Human closure uses one completed case Task with matching contained Provenance and explicit evidence or exception.
- FHIR intake, deterministic staff-inbox replacement, exact request/response logs and thread-safe state. Receiving a proposal never automatically accepts it.
- Validated scenario loading, real-time supervised replay, progress, cancellation and API-driven headless operation; no extra headless flag is needed.
- Four localized screens: scenario runner, staff inbox, FHIR log and explicit source-human actions.

**Design gaps completed**

- C6 / decision 16: Done. Consultation, referral and appointment actions have endpoints and forms; headless replay sends hospital-system steps without the ClientApp.
- C20: Done. Screening reports contain a synthetic Patient with display name, MRN, language and contact points.
- C21: Done. Outcomes carry the WS3 evidence-kind code; clinical values remain human/source inputs, not simulator-generated decisions.
- C11: Done. `ActingStaff` and the action forms support `nurse-navigator`, `receiving-clinician` and `lab`.
- Decision 4: Done. The HIS receives approved DocumentReferences only and calls CareLoop exclusively through `/fhir`.
- C13 / decision 7: Done for WS7. Consent grant/withdrawal builders and `consent-recorded` actions carry synthetic caregiver contacts; appointments include the Location participant. Shared schema and consent scenarios remain WS10.
- WS7 contract points: Done. Id-bearing PUT, `his-simulator` source, reviewed-urgency extension, simulated metadata tag, staff-notice Tasks and `other` contact points match WS3/WS4.

**Validation and remaining boundaries**

- 189 HIS backend tests and 36 UI tests pass with no skips/todos; 161 existing CareLoop FHIR/provider regression tests passed earlier. Frontend lint, type-check, production build and formatting pass.
- Real-host/browser checks cover replay, manual actions, refused outcomes, evidence-backed closure, inbox/log views and mobile layout against a local synthetic fake CareLoop upstream. This is not a real API/database patient journey; Docker was not built.
- Cohort starts are explicitly unsupported pending WS10. Patient-channel behaviour is WS8; shared scenario corrections (including the happy-path evidence placeholder) and full end-to-end journeys remain WS10. See the [HIS guide](../src/simulators/his/README.md) and [manual HTTP harness](../src/simulators/his/HisSimulator/HisSimulator.http).

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
- C10: a `rate-confidence` patient behaviour in `scenario.schema.json` (agree with WS7 and WS8).
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
| `PUT` route shape | WS3, WS7, WS8 | WS3 and WS7 implement `PUT /fhir/{resourceType}/{id}`; URL and body ids must match or the request is refused with 400. CareLoop's outbound sends use the same shape. `AGENTS.md` is aligned; WS8 confirmation remains open. |
| `Communication.category` codes | WS4, WS8 | Both sides use system `https://careloop.invalid/fhir/CodeSystem/communication-category`. WS8 proposes codes `delivery-status`, `acknowledgement`, `teach-back`, `call-me`, plus `confidence-rating` (decision 3, C10); `StatusMap` has all five. WS8 to confirm. |
| External system identifiers | WS2, WS3, WS7 | Settled and built: WS7 sends `meta.source = his-simulator`, matching the development `ExternalSystems:Systems` key. WS3 refuses missing source with 400 and unconfigured source with 403. The case retains that source system and replies are routed to it. |
| Scenario patient behaviour | WS7, WS8, WS10 | A scenario's steps are hospital-system events only. How a patient reacts is `patients[].behaviour` (`delivery`, `response`, `respondAfter`, `respondToNotification`), applied by the patient-channel simulator when a notification arrives. Cohort runs need a way to give each generated patient their scenario's behaviour. |
| Time in a live run | WS1, WS5, WS7, WS8, WS10 | CareLoop runs on real time. A live run uses the demo pathway folder (`Pathways:Directory`), whose deadlines are divided by 1800, and both simulators pace scenarios with `Scenarios:RealSecondsPerScenarioHour` = 2. The three figures must stay equal. End-to-end tests should assert outcomes, not exact times, and leave headroom for slow machines. |
| Simulator provider wire format | WS4, WS8 | Settled by WS4 (`SimulatorNotificationProvider`): `POST {PatientChannelBaseUrl}/fhir/CommunicationRequest` with the attempt identifier as the `https://careloop.invalid/fhir/sid/notification-attempt` identifier and as the id (prefixed `caregiver-` for a caregiver message), category `patient` or `caregiver` in `https://careloop.invalid/fhir/CodeSystem/recipient-kind` (a caregiver message belongs in the Email inbox), `recipient` naming the inbox, `about` the case `Task`, `payload` the text and `language`. The simulator answers with a `Communication` to `POST /notifications/inbound/simulator` whose `basedOn` is that request and `about` the case `Task`; reports based on a caregiver request are ignored. Each post must carry `X-CareLoop-Signature: sha256=` plus the lower-case hex HMAC-SHA256 of the raw UTF-8 body, keyed with the secret shared through `SIMULATOR_INBOUND_SECRET` (`Notifications:Simulator:InboundSecret` in the API); anything unsigned or wrongly signed gets 401. WS8 to sign in `CareLoopFhirClient.SendCommunicationAsync` and to confirm the `Communication.category` codes. |
| Patient replies beyond acknowledgement | WS1, WS4, WS8 | Settled (decisions 2 and 3): the replies are acknowledgement, teach-back, call-me and confidence rating. Accepting an appointment is an acknowledgement; a patient reply never changes appointment status. |
| Simulated marker on FHIR resources | WS7, WS8 | WS7 sends `meta.tag` in `https://careloop.invalid/fhir/CodeSystem/data-origin` with code `simulated`, matching the API samples. WS8 confirmation remains open. CareLoop neither requires nor emits a marker. |
| Staff notification transport | WS4, WS7 | Settled and built: WS4 sends a `staff-notice` Task with `PUT {StaffInboxBaseUrl}/fhir/Task/staff-notice-{attempt}`; `owner` names the staff inbox, `focus` the case Task and `description` the text. WS7 accepts that PUT and upserts the inbox item; the obsolete plain notification POST was removed. |
| Urgency on the review `Task` | WS3, WS7 | Settled and built: WS7 carries source-reviewed urgency unchanged in `https://careloop.invalid/fhir/StructureDefinition/reviewed-urgency` as `valueCode`, because placeholder codes do not fit FHIR priority. WS3 reads the extension first and retains its existing `Task.priority` fallback. |
| Nine event types | WS2, WS3 | Delivery reports and patient responses share one event, `NotificationUpdated`. Caregiver consent is its own event, `ConsentRecorded` (C13), so there are exactly nine source events and handlers. |
| Internal events | WS2, WS5, WS6, WS9 | `DeadlineElapsed`, `HandoffSummaryApproved` and `EscalationAcknowledged` are saved through `ICaseRepository.SaveAsync` directly, not through a handler. |
| Contact point channel | WS3, WS4, WS7, WS8 | Settled and built through WS7: `ContactPoint.system = other` maps to the simulator channel; WS7 sends it for Patient and RelatedPerson contacts. `sms` and `email` stay unreachable while only the simulator provider is enabled. WS4 carries recipient kind; the caregiver Email inbox remains WS8. |
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
| Caregiver appointment message | WS1, WS2, WS3, WS4, WS7 | Settled and built: domain event `AppointmentScheduled` (`ScheduledFor`, `Location`) is raised on booking, rebooking and time changes; WS7 sends the Location participant and WS3 reads its `actor.display`. WS4 notifies the caregiver only while consent is active; `FhirPublisher` ignores the event. A booking without a time raises nothing. |
| Acknowledgement in escalation notices | WS1, WS4, WS5 | Settled and built (WS1, WS4): `EscalationRaised.AcknowledgedByRole` and `AcknowledgedAt`, filled by `Case.Escalate` from the previous level, so WS5 only calls `Escalate`; WS4's staff templates show them through `{{previousAcknowledgement}}`. |
| Escalation identifier | WS1, WS2, WS6 | Settled and built (WS1, WS2): `Escalation.Id` (uuid), carried on `EscalationRaised.EscalationId`, persisted unique; `Case.AcknowledgeEscalation(escalationId, ...)` refuses a superseded escalation. WS6's acknowledgement endpoint takes it as `{escalationId}`. |
| Read audit | WS2, WS6 | Settled and built (WS2): staff reads are recorded in `CaseReads` (`CaseReadRecord`), not in `AuditEvents`, because a read changes no case state and has no source event. WS6 writes one per read (C4, C19). |
| Escalation acknowledgement | WS1, WS2, WS5, WS6 | Settled (decision 13): C15. Only the role the escalation went to may acknowledge; it is audited and never clears an exception, changes the owner or stops the clock. |
| Patient identifiers | WS1, WS2, WS3, WS6, WS7 | Settled (decision 15): C20. The screening result carries the patient's display name and MRN; the case stores them; staff roles read them, `coordinator` does not. `AGENTS.md` changes (shared; agree first). |
| Display labels | WS1, WS6, WS10 | Settled (decision 19): the ten labels are computed by a Domain function and returned on reads and in metrics; the web app never recomputes them. |
| Evidence kinds | WS1, WS2, WS3, WS6, WS7, WS10 | Settled (decision 20): C21. The pathway declares the kinds (placeholders); intake rejects others; a `treatment-plan` outcome starts the `start-treatment` deadline. The PM confirms the names; WS3 and WS7 agree the FHIR code mapping. |
| Reply deadline placeholders | WS1 | Settled (decision 18): the pathway file follows screen 4 of the design. Clock time, not working hours; a working-hours calendar is future work. |
| HIS simulator screens | WS7, WS10 | WS7 built the four screens and API-driven headless replay (decision 16, C6). The full headless patient journey demo remains WS10. |

## Design gap tracker

One row per item and workstream, so no part is lost between owners. **Open** means not started or not merged;
**Done** means built, tested and merged on `main` (or on this branch's pull request). Change a row only in the change
that finishes it. Items come from [`ui-design-gap-analysis.md`](ui-design-gap-analysis.md); C14 and C16 to C18 were
withdrawn.

| Item | WS | Part | Status |
|---|---|---|---|
| C1 Programmes read model | WS6 | `GET /programmes`, `GET /programmes/{pathwayId}`, `ProgrammeResponse` | Done |
| C1 | WS6 | Web: programmes screen (screen 4, part of C9) | Done |
| C2 Pathway citations and change log | WS1 | `citation`, `changeLog`, `ownerRole` in both pathway files | Done |
| C3 Source system on the timeline | WS6 | `TimelineEntryResponse.SourceSystem` via `SourceEvent` | Done |
| C4 Team-scoped, audited reads | WS2 | `CaseReads` table and `CaseReadRecord` | Done |
| C4 | WS6 | `facilityId` filter, out-of-team refusal, required `reason`, one `CaseReadRecord` per read | Done |
| C4 | WS6 (shared) | `X-Demo-Facility` in `DemoRoles` | Done |
| C5 Screen 3 measures | WS6 | `MetricsResponse` measures with denominators, incl. display-label counts and C21 treatment measures | Done |
| C5 | WS10 | `SimulationArmResponse` with the same measures | Open |
| C6 HIS clinician actions and headless mode | WS7 | `/actions/consultation-request`, `/consultation-answer`, `/referral`, `/appointment`, forms, API-driven headless replay | Done |
| C7 SMS reply codes | WS8 | `1` acknowledge, `2` call-me on the `Sms` inbox | Open |
| C8 Exception-path and demo scenarios | WS10 | Six exception scenarios with `expect` entries; demo scenario for screens 15, 16, 19 to 21 | Open |
| C8 | WS10 | Replace `PLACEHOLDER-EVIDENCE-KIND` in `happy-path.json` with a declared kind (C21) | Open |
| C9 Dashboard | WS6 | Screens 1 to 4, 17, 18; summary draft, edit-on-approve, reject; escalation acknowledge; other-cases card | Done |
| C10 Confidence rating | WS1, WS2, WS3 | Domain, payload, handler, `StatusMap` | Done |
| C10 | WS4 | `SimulatorInboundNotificationHandler` maps `confidence-rating` | Done |
| C10 | WS6 | `confidenceRate` on `MetricsResponse` | Done |
| C10 | WS8 | Inbox rating action and `confidence-rating` `Communication` builder | Open |
| C10 | WS10 | `rate-confidence` behaviour in `scenario.schema.json` | Open |
| C11 Roles | WS1 | Pathway `allowedRoles`, `authorizedRoles`, `OwnershipRules` | Done |
| C11 | WS6 (shared) | `nurse-navigator`, `receiving-clinician`, `lab` in `DemoRoles.HumanRoles` | Done |
| C11 | WS7 | `ActingStaff` roles and clinician actions page | Done |
| C12 Per-kind reply deadlines | WS1, WS2 | Pathway deadlines and next actions | Done |
| C12 | WS5 | Ordinary deadlines in `ComputeDueAt`, clock time | Done |
| C13 Caregiver and consent | WS1, WS2, WS3 | `Consent`, `ConsentRecorded`, `ConsentMapper`; `AppointmentScheduled`, `Location` on payload and mapper | Done |
| C13 | WS4 | Route `AppointmentScheduled` to a consented caregiver; caregiver templates (standard and SMS-safe); consent checked at send | Done |
| C13 | WS7 | `Consent` builder and `HisEventKind`; `Location` participant on `AppointmentBuilder` | Done |
| C13 | WS8 | `Email` in `InboxChannel`; recognise caregiver messages | Open |
| C13 | WS10 | Consent `action` in the schema; consented and withdrawn scenarios | Open |
| C13 | Shared | `AGENTS.md` events table row for `AppointmentScheduled` | Done |
| C15 Escalation acknowledgement | WS1, WS2 | `AcknowledgeEscalation`, persistence, `Escalation.Id`, `EscalationRaised` acknowledgement fields | Done |
| C15 | WS4 | Staff template placeholder for the previous level's acknowledgement | Done |
| C15 | WS5 | Sweep keeps escalating after acknowledgement, through `Case.Escalate` | Done |
| C15 | WS6 | `POST /cases/{id}/escalations/{escalationId}/acknowledgement`, timeline entry, escalations on `CaseDetailResponse` | Done |
| C19 Patient's other cases | WS6 | `patientReference` filter for clinician roles, audited in `CaseReads` | Done |
| C20 Patient identifiers | WS1, WS2, WS3 | Case fields, payload, `DiagnosticReportMapper` | Done |
| C20 | WS6 | Name and MRN on staff reads, null for `coordinator` | Done |
| C20 | WS7 | `DiagnosticReport` with a synthetic `Patient` | Done |
| C21 Evidence kinds | WS1, WS2, WS3 | Pathway vocabulary, intake rejection, mapper | Done |
| C21 | WS5 | `start-treatment` handled like any other deadline | Done |
| C21 | WS7 | Outcome resources carry a declared kind | Done |
| Decision 19 Display labels | WS1 | `CaseDisplayLabels.From` | Done |
| Decision 19 | WS6 | `DisplayLabel` on case reads and in metrics | Done |
| Decision 19 | WS10 | Journey tests assert the label at each happy-path step | Open |
| Summary approver roles | WS1, WS9 | `summaryApproverRoles` in the pathway (open contract point) | Open (decision needed) |

