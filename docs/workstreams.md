# Workstreams

Ten workstreams build CareLoop in parallel. This file says, for each one, which files it owns, what it depends on and
which stubs it implements. The earlier component register, which maps each component to its user stories and functional
requirements, is kept in [`component-register.md`](component-register.md). Find your stubs with:

```bash
grep -rn "TODO(WS-4)" --include="*.cs" --include="*.ts" --include="*.tsx" .
```

A second list, **Design gaps**, appears under some workstreams. These come from
[`ui-design-gap-analysis.md`](ui-design-gap-analysis.md) (items C1 to C22), which compares the screen designs with the
code. They are additive. The decisions in that document's section 5 (1 to 7 and 12 to 20) were taken on 13 November 2026,
and decisions 21 to 30 (text message and email only, C22) on 16 November 2026, decision 31 (reply 1 means "I got this message") and decisions 32 to 36 (call-backs to the nurse, the patient's
appointment message, event origin, coordinator redaction, staff message wording) on 17 November 2026.
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
| 8 | Messaging simulator | WS3 (FHIR contract and codes), WS4 (notification delivery) | Done |
| 9 | AI texts: role briefing, impact summary, patient message draft | WS2, WS6 | Open |
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
- `src/api/CareLoop.Api/Infrastructure/Persistence/**` (the WS9 exceptions, `Summaries/` and `Configurations/HandoffSummaryRecordConfiguration.cs`, were removed with the handoff summary on 19 November 2026)
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
- Migrations (14 November 2026): the migrations were squashed into a single `InitialCreate` holding the whole current schema, including the WS9 `HandoffSummaries` provenance columns and the outbox delivery lease (`OutboxDeliveries.LeaseOwner`, `LeasedUntil`, added for WS4 and regenerated into the same `InitialCreate`). Up and down were run against PostgreSQL 14 before the lease columns were added; the regenerated migration differs only by those two nullable columns. A database created from an earlier migration must be dropped and recreated (`docker compose down -v`); there is no upgrade path from the old history. Add later schema changes as new migrations after `InitialCreate`. The first is `RemoveHandoffSummaries` (19 November 2026): it only drops the `HandoffSummaries` table.
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

- C10 (superseded by C22: no `Communication` reaches CareLoop any more): map `Communication.category` `confidence-rating` to `NotificationUpdateKind.ConfidenceRated` in `StatusMap.ToNotificationUpdateKind`, and carry the 1 to 5 value into `NotificationUpdatePayload.Confidence` (the handler refuses a missing or out-of-range value). Done on the FHIR side: `StatusMap.ToConfidence` reads the value from the first `payload.contentString`; WS4's `SimulatorInboundNotificationHandler` has to call it, since patient replies do not cross the FHIR boundary.
- C22 (decisions 21, 22): `StatusMap.ToNotificationChannel` maps `sms` and `email` only; `other` and every other system map to nothing. `ToNotificationUpdateKind`, `ToConfidence` and the `communication-category` code system are removed, and the `Communication` and `CommunicationRequest` samples with them; the `Patient`, `DiagnosticReport` and `Consent` samples use `sms` and `email` contact points. Done.
- C13: add a seventh inbound mapper, `ConsentMapper`, that turns a FHIR `Consent` resource into a `ConsentRecorded` envelope with a `ConsentPayload`: `status` `active` is `Granted` (with the caregiver's contact point read from the resource), `inactive` or `rejected` is `Withdrawn`; the resource identifier plus version is the source event ID. Register it with the other mappers. Done: the caregiver is a contained `RelatedPerson` named in `provision.actor`, and the consent names its screening result through the `screening-result` extension.
- C20: `DiagnosticReportMapper` reads the patient's display name and MRN from the referenced FHIR `Patient` (contained or resolved through `FhirHttpClient`) into `ScreeningResultPayload`; a sample with a `Patient` in `Fhir/Samples/`. Done: a contained `Patient` (`subject` = `#id`) is read directly; a literal `Patient/{id}` is read from the source system when it is configured, and otherwise the case opens without demographics. `Patient.json` and the contained patient in `DiagnosticReport.json` are the samples.
- C21: `Procedure` and follow-up `DiagnosticReport` mappers set `OutcomePayload.EvidenceKind` to one of the pathway's declared kinds; agree the FHIR code to kind mapping in `StatusMap`. Done: the kind is the code in `https://careloop.invalid/fhir/CodeSystem/evidence-kind` on `Procedure.code` or the follow-up report's `code`, passed through unchanged; WS7 sends the pathway's kind names there.
- C13: `AppointmentMapper` reads the place into `AppointmentUpdatePayload.Location` from the participant whose actor is a `Location` (`Location/...` reference, actor `type` `Location`, or a contained `Location`): `actor.display`, else the contained `Location.name`. `FhirPublisher` publishes nothing for `AppointmentScheduled`. Done; `Samples/Appointment.json` carries a synthetic `Location` participant.
- Review follow-up (done): a `meta.source` not configured under `ExternalSystems:Systems` is refused with 403; attendance and a missed appointment are dated at the appointment (`end`, else `start`), a booking when it was made; a caregiver's contact point uses `telecom` system `other`, like the patient's (since C22: `sms` or `email`); an `active` `Consent` with a `deny` provision is a withdrawal, and a staff `performer` is recorded as its actor; the approved summary's `DocumentReference` is sent on `HandoffSummaryApproved`, not on `HandoffProposed`. From the PR review: an appointment's time is `meta.lastUpdated` first; a closure's `Provenance` counts only when it targets that case `Task`; a handoff rejection is published as the case `Task` with status `rejected` and the reason, unless a newer handoff is pending. `AGENTS.md` (shared) now shows the `PUT` route, the `Consent` and `Patient` mappings and these rules.

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
- `SmsNotificationProvider` and `EmailNotificationProvider` (built under C22; they were stubs until then)
- Set `Outbox:Enabled` to `true` in `appsettings.json` when the dispatcher works

**Design gaps**

- C10 (decision 3; superseded by C22, where no reply is ever a rating): in `SimulatorInboundNotificationHandler`, map `Communication.category` `confidence-rating` to `NotificationUpdateKind.ConfidenceRated` and the 1 to 5 value into `NotificationUpdatePayload.Confidence`, by calling WS3's `StatusMap.ToNotificationUpdateKind` and `StatusMap.ToConfidence`. A rating is a measure, never an acknowledgement. Done.
- C13 (decision 7): `RecipientResolver` sends a caregiver with active consent the appointment date, time and place only, never the result. Add a caregiver template under `Templates/en/`, with a standard and an SMS-safe variant. Record each send as a `NotificationAttempt`. Check `Consent.IsActive` when the message is sent, not only when the event was raised, because the outbox may deliver after a withdrawal; a withdrawal stops every later send. `SimulatorNotificationProvider` marks the `CommunicationRequest` as a caregiver message on the `Email` channel, so the messaging simulator files it in its Email inbox (WS8). `EmailNotificationProvider` stays a stub. `Consent`, `RecipientKind.Caregiver`, `ConsentHandler` and `ConsentMapper` are done. Unblocked: route the new `AppointmentScheduled` domain event (`ScheduledFor`, `Location`, which may be null; WS1) to the caregiver, and to nobody else, while `Case.CaregiverConsent.IsActive`. The template shows date, time and place only; when `Location` is null it says the place will be confirmed rather than inventing one. Done when: `NotificationRouter` handles `AppointmentScheduled`; a test covers send with active consent, no send with none or withdrawn consent (including a withdrawal saved after the event was raised), the SMS-safe variant, and a `NotificationAttempt` per send. Done: templates `caregiver-appointment` and `caregiver-appointment-place-tbc`, whose SMS-safe variants leave out the place (a clinic name can say what the appointment is for). The caregiver's language is the patient's, falling back to English. Caregiver attempts are not recorded on the case's notification track, which follows the patient; the caregiver `CommunicationRequest` id is prefixed `caregiver-`, and reports based on it are ignored, so a caregiver's delivery or acknowledgement never counts as the patient's. A booking without a time raises no `AppointmentScheduled` (WS2), so nobody is told a time the source never gave.
- C15 (decision 13): each `EscalationRaised` staff notice says who acknowledged the previous level, if anyone (WS5 sends the event). `EscalationRaised.AcknowledgedByRole` and `AcknowledgedAt` now exist (WS1, filled by `Case.Escalate`). Add a placeholder to the staff template that renders, for example, "Level 1 was acknowledged by facility-lead at 09:40", and nothing when both are null. Staff notices still carry no patient detail. Done when: a template test covers both cases. Done: `{{previousAcknowledgement}}` in both staff templates, filled by `NotificationRouter.PreviousAcknowledgementFor` (for example "Level 1 was acknowledged by facility-lead at 09:40 UTC on 2026-11-14.").
- C22 (decisions 21 to 30): text message and email only. Done:
  - `NotificationChannel` is `Sms` and `Email`; migration `SmsAndEmailChannelsOnly` rewrites stored `Simulator` values as `Sms`. `NotificationUpdatePayload.Channel` is optional: null when a person recorded the outcome of a call.
  - `SmsNotificationProvider` and `EmailNotificationProvider` share `SimulatorNotificationProvider`, which posts `{ messageId, channel, to, subject, body, language, metadata }` as JSON to `{Notifications:<channel>:BaseUrl}/messages`. `metadata` carries the case and the audience (patient, caregiver or staff).
  - `SmsInboundNotificationHandler` and `EmailInboundNotificationHandler` share `SimulatorInboundNotificationHandler`, at `POST /notifications/inbound/sms` and `/email`: signed delivery reports and replies. `1` is `Acknowledged`; `2` and any other text are `CallRequested`. A reply must come from one of the patient's own addresses on that channel; a callback about a caregiver or staff message changes nothing.
  - Staff contacts come from `Notifications:StaffContacts`, one address per role, with an optional `default`. `EnabledProviders` and `PerEventOverrides` may name only `Sms` and `Email`, checked at start-up.
  - Templates: patient messages ask for a reply of 1 or 2 and carry no link; the caregiver SMS variants now say the place too, so both variants say the same.
- Review of PR 43 (decisions 31 to 36). Done:
  - Every channel a patient notification goes out on is recorded on the track as sent (`NotificationUpdateKind.Sent`) before anything is sent, so one channel's failure marks the patient unreachable only once every channel has failed; each refusal is fed back as it is.
  - `CallRequested` goes to the role the pathway names for the call (`CallerRole`, the nurse navigator), not the case owner.
  - `AppointmentScheduled` also goes to the patient (`patient-appointment`, `patient-appointment-place-tbc`) with the reply codes; a reply to it counts, its delivery reports are ignored.
  - Staff messages: "A case at {facility} needs your action. Open CareLoop and look for case {number}." The escalation line says who acknowledged the previous level in plain words and local time. `Notifications:TimeZone` and `Notifications:FacilityNames` (`MessageFormat`); an unknown time zone stops start-up.
  - Patient messages use the approved wording: the follow-up is T05 (care-team callback request) and the appointment message is T01 (appointment confirmation), each with "Dear patient," in place of the name, the reply line and the reference added, and the facility's contact from `Notifications:FacilityContacts`. The adaptations still need the approver's sign-off.
  - The inbound endpoint also accepts a channel enabled only by a per-event override; provider names must be exactly `Sms` or `Email`.
  - Each applied notification update has a plain-words summary on the timeline, and the callbacks carry `Origin` `sms` or `email`.
- Decision 2: a patient reply never changes appointment status. "I will come" arrives as `acknowledgement` and is translated to `Acknowledged` only.
- Contract points settled by WS4: "Staff notification transport" and "Simulator provider wire format" (see the table below; both changed under C22). Language: a template missing in the recipient's language falls back to English (`TemplateRenderer.FallbackLanguage`).
- Delivery guarantees: several API instances can run the dispatcher; each delivery is claimed with one conditional update (`OutboxDelivery.LeaseOwner`, `LeasedUntil`, `Outbox:LeaseDuration`) and only the claim owner records the outcome. A staff, caregiver or patient-appointment send a provider refused makes the router throw, so the outbox retries it (then parks it); a refused patient follow-up is recorded on the case as a failed attempt.
- Known limits: `NotificationSendLog` (which remembers accepted attempts so a retry resends only failed channels) is in memory, so a restart, or a claim taken over by another instance, can send an accepted message again; attempt identifiers are deterministic, so the receiver can drop the repeat. A durable record needs a WS2 table. Messages give local time in one configured zone (`Notifications:TimeZone`); a deployment spanning time zones would need one per facility.

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
- C3: `SourceSystem` on `TimelineEntryResponse`. The timeline reads `AuditEvents`, which do not hold the source system: join `AuditEvent.SourceEventId` to `SourceEvent.SourceSystem`; internal events (`DeadlineElapsed`, `EscalationAcknowledged`) show CareLoop as their source.
- C4: `facilityId` filter on `GET /cases`, refusal of case reads outside the caller's facility, a read audit per read, and `X-Demo-Facility` in `DemoRoles` (shared file; agree first). Added (decision 12): a read outside the caller's team requires a `reason` query parameter, stored in the same audit entry as the view. Storage exists: append a `CaseReadRecord` to `CareLoopDbContext.CaseReads` (WS2) for `GET /cases/{id}`, `/timeline` and filtered lists, with `OutsideTeam` and `Reason`. Done when: tests cover an in-team read, a refused out-of-team read without a reason, an allowed one with a reason, and one record per read.
- C5: the screen 3 measures on `MetricsResponse`, each with its denominator (with WS10 for `SimulationArmResponse`): time-to-step medians, understanding rate, closures by reason, cases reaching each display label (decision 19), referral accepted and attended, appointments attended, treatment started by its deadline and time to care started (both from the C21 evidence kinds: denominator is cases with a `treatment-plan` outcome, numerator a `treatment-started` outcome before the `start-treatment` due time).
- C9: build screens 1 to 4, 17 and 18 of the design in `src/web/` (decision 17), against the endpoints above. Read-only except: the escalation screen acknowledges (C15); the other-cases card for clinician roles (C19). Show the display label the API returns (decision 19); do not recompute it.
- C10: `confidenceRate` on `MetricsResponse`.
- C11: add `nurse-navigator`, `receiving-clinician` and `lab` to `DemoRoles.HumanRoles` (shared file; agree first).
- C15 (decision 13): `POST /cases/{id}/escalations/{escalationId}/acknowledgement` (role from the demo headers) and the acknowledgement as a timeline entry and on `CaseDetailResponse`. `{escalationId}` is `Escalation.Id`; call `Case.AcknowledgeEscalation(escalationId, actor, at)` and map its refusals (wrong role, superseded, already acknowledged) to 409 or 403. The endpoint saves an `EscalationAcknowledged` internal event through `ICaseRepository.SaveAsync`, like `DeadlineElapsed`. `CaseDetailResponse` lists escalations with id, level, escalated-to role, raised at, acknowledged-by role and time, and resolved at. Only the role the escalation went to may acknowledge (the Domain enforces it), and a non-coordinator must also act in the facility it was escalated to: the endpoint refuses another facility's lead with 403 even when a cross-facility reason is given. (The Domain does not repeat the facility check because coordinators act without a facility; moving it into `Case` needs a rule for them first.) Acknowledging never clears an exception, changes the owner or stops the clock.
- C19: `patientReference` filter on `GET /cases`, allowed for `clinician` and `receiving-clinician` only, refused for other roles, audited as a read under C4 (`View` `case-list`, `Query` naming the filter).
- C20: `PatientDisplayName` and `PatientSecondaryIdentifier` on `CaseSummaryResponse`, `CaseDetailResponse` and `ExceptionItemResponse` for staff roles only; null for `coordinator`, whose screens show no names.
- Decision 19: `DisplayLabel` on `CaseSummaryResponse` and `CaseDetailResponse`, from `CaseDisplayLabels.From`.

**Completion**

All WS6 portions of C1, C3, C4, C5, C9, C10, C11, C15, C19, C20 and decision 19 are **Done**.
The nurse-summary case-read enhancement is **Done**: `GET /cases` returns `CaseDetailResponse` rows,
matching the single-case read, with nullable `PatientLanguage` from the latest applied screening event.
Language is loaded per page and redacted for coordinators; the AI fact boundary is unchanged.
Facility is the current team boundary; cross-facility reads require a recorded reason. Coordinators have global
aggregate measures but case reads only for open coordinator escalations, with names/MRNs redacted. This follows the
design's "Who sees what" table (a coordinator sees one case only when it is escalated to them) and is a requirement
of this workstream, not only a completion note; the review's request to confirm it is recorded as a decision for the
product owner.
C22 (decisions 24, 30): `POST /cases/{caseId}/call-outcome` lets a nurse navigator record, after a call, that the
patient will come, that they said the next step back, and a 1 to 5 confidence rating. Each outcome is a
`NotificationUpdated` event with the nurse as actor and no channel, sent through `EventIngestor`; other roles get 403 and
recording never changes the owner. On the case screen, the "After a call with the patient" card does the same, and the
timeline labels each entry from its `origin` ("Text message", "Email", "CareLoop", "Hospital system"). Done.

Review of PR 43 (decisions 32, 34, 35). Done: recording a call answers a pending call-back and resolves its
escalations (a rating alone puts the case back to waiting for the acknowledgement); the access is written to the read
audit with its reason; the case's `patientReference` and `sourceResultId` are null for the coordinator, and the screens
leave out "Result … from the hospital system" and the other-screening card without them. The card keeps focus (the
button is `aria-disabled` rather than disabled; focus goes to the heading after a success and to the message after a
refusal), ties its hints to the controls, says why it is locked before the controls, keeps 1 to 5 on one row, says what
was just recorded, and on a 409 reads the case and timeline again. The footnotes no longer say "Read-only".

Live QA of 18 November (M2, H3). Done: the coordinator's case and timeline reads also leave out the outcome evidence
reference and every timeline `evidenceReference`, and read `(hidden)` wherever an entry's wording or actor quotes one of
the case's identifiers. Impact's cases reaching each display label now count a label once the event that puts a case
there was applied, even if the case moved on at once (result received, case opened, outcome recorded), and still count
an optional step only where it happened.

Read lists are resilient to inconsistent data: an exception row with no deadline or next action is returned with
those fields null, and a metric leaves out (and logs) a case whose history cannot support it, instead of failing the
whole read. `GET /exceptions` accepts `skip` and `take` (default and maximum 200).
Known design question for WS1: once a level 2 escalation exists, level 1 can no longer be acknowledged (superseded).
Confidence is rating participation with the latest-rating average separately reported; all durations are clock time.
Historical metrics use applied snapshots and the shared Domain display-label rule.

Validation: 560 API tests (34 skipped, all WS9/WS10) passed. The dashboard is the designed one from PR #29 (the web app
was given to Birir at the 13 November sync), connected to these endpoints; it replaces the first dashboard, whose original
25 tests were ported to its Vitest suite. 50 screen tests (Testing Library and user-event in jsdom, against a fake API at
the `fetch` level) cover the handoff summary, escalation, case, worklist and identity screens, including D1, D3 and D5,
for 181 Vitest tests in all. Lint, type-check, unit tests and build run in CI.
Synthetic browser interception verified acknowledgement, role redaction, mobile layout, programmes/impact and exact
edited-text approval. WS9 summary persistence/provider endpoints and WS10 real end-to-end/comparison remain Open;
the completed summary UI explicitly shows backend 501/unavailable errors. (Since 19 November 2026 the handoff summary
screen and its tests are removed; the dashboard shows the three AI texts instead: the role briefing on the worklist, the impact summary on the impact page and the patient message draft on the case page (see WS9). The
counts above are from before that change.) Eight moderate development-only parser/
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
- WS7 contract points: Done. Id-bearing PUT, `his-simulator` source, reviewed-urgency extension and simulated metadata tag match WS3/WS4.
- C22 (decisions 21, 28): Done. Patient contact points are `sms` (or `email`, `Contacts:PatientChannel`), caregivers' `email`; the value is still the synthetic id until scenarios carry addresses (WS10). The `staff-notice` Task and `CommunicationRequest` receipt are removed: staff notices go by email or text to the messaging simulator. The staff inbox keeps what CareLoop sends as FHIR, and an unrecognised Task is filed as "Other case update".

**Validation and remaining boundaries**

- 189 HIS backend tests and 36 UI tests pass with no skips/todos; 161 existing CareLoop FHIR/provider regression tests passed earlier. Frontend lint, type-check, production build and formatting pass.
- Real-host/browser checks cover replay, manual actions, refused outcomes, evidence-backed closure, inbox/log views and mobile layout against a local synthetic fake CareLoop upstream. This is not a real API/database patient journey; Docker was not built.
- Cohort starts are explicitly unsupported pending WS10. Patient behaviour is WS8 (the messaging simulator); shared scenario corrections (including the happy-path evidence placeholder) and full end-to-end journeys remain WS10. See the [HIS guide](../src/simulators/his/README.md) and [manual HTTP harness](../src/simulators/his/HisSimulator/HisSimulator.http).

## WS8 Messaging simulator

**Owns**

- `src/simulators/messaging/**`, `tests/simulators/messaging/**`

**Stubs to implement**

- `Inbound/`: `CommunicationRequestReceiver`, `CommunicationRequestMapper`, `DeliverySimulator`, `AutomaticResponder`, and the inbox, patient behaviour, message log and case status endpoints
- `Outbound/`: the six `Communication` builders (the sixth is the confidence rating, C10), `CareLoopFhirClient`, `PatientResponseSender`, `CaseStatusReader`
- `Scenarios/`: `ScenarioLoader`, `ScenarioRunner`, scenario endpoints
- `State/SimulatorStore.cs`
- `ClientApp/src/features/*`: inbox, case status, patient behaviours, scenario runner, message log

**Design gaps**

- Rename (Done): the patient-channel simulator is now the messaging simulator. Folder `src/simulators/messaging`, project and namespace `MessagingSimulator`, tests `tests/simulators/messaging/MessagingSimulator.Tests`, Docker Compose service and health name `messaging-simulator`, ClientApp package `messaging-simulator-client`. The API's `Notifications:Sms:BaseUrl` and `Notifications:Email:BaseUrl` point at the new service name; nothing else in the API changes.
- Review of PR 43 (Done): times read as on the dashboard (`17 Nov 2026, 00:11`, 24-hour, Abu Dhabi time), and a record's time sits under its text so "What CareLoop is told" keeps its width in a narrow column.
- C22 (Done, decisions 21 to 30): the simulator now handles text messages and email only (it was renamed the messaging simulator in a later change). `POST /messages` takes plain JSON from CareLoop's `Sms` and `Email` providers; delivery reports and replies go back as signed JSON to `{CareLoop:CallbackBaseUrl}/sms` or `/email`. Inboxes are the patient's text messages, the patient's email, caregiver and staff; only a patient's message can be replied to, and a reply is forwarded as typed. Removed: the `App` and `Portal` channels, the smartphone view, the case-status view and its FHIR read, the teach-back and rating actions and behaviours, every FHIR builder and the Firely package. Behaviours are found by the message's address. A scenario using `understand`, `rate-confidence` or `confidence` is refused (schema change is WS10).
- C7 (Done; since C22 CareLoop, not the simulator, reads the reply): on the `Sms` channel accept an inbound reply of `1` as acknowledge and `2` as call-me (screen 9). Built as `POST /api/inbox/{messageId}/reply`; any other text is refused and nothing is sent. CareLoop sends no channel, so a patient message lands in the `Sms` inbox when its inbox address starts with `SYN-SMS-` (`SYN-PORTAL-` for the portal; anything else goes to `PatientChannel:DefaultChannel`, `App`). The SMS-safe template is not shown there, because the simulator provider allows sensitive content and so always receives the standard text.
- C10 (Done, decision 3; removed under C22, a nurse records the rating): a confidence rating action (1 to 5) on the inbox and a sixth `Communication` builder with category `confidence-rating`. A rating is not an acknowledgement. Built as `POST /api/inbox/{messageId}/rate-confidence`; a patient behaviour can also rate (`rate-confidence`, or a `confidence` beside another response).
- C13 (Done, decision 7): an `Email` value in `InboxChannel` so caregiver messages are shown, not sent. Since C22 a caregiver message is recognised by its `caregiver` audience in the metadata. A caregiver message is recognised by its `caregiver` recipient kind; it gets a delivery report, is never answered and cannot be acted on.
- Decision 2 (Done): "I will come" sends an `acknowledgement` `Communication` only. There is no appointment-confirmation reply.
- Contract points (see the table below): WS8 has confirmed the `Communication.category` codes, the simulator provider wire format and the simulated marker, and sends no `PUT`.

## WS9 AI texts: role briefing, impact summary, patient message draft

> **Changed on 19 November 2026.** WS9 was the AI handoff summary (FR-11). That is removed: its endpoints, contracts, table, domain event and screen are gone. The API now asks the agents in a Microsoft Foundry project directly for three texts shown to staff, one agent per text, called by name. The Python AI workflow service is retired and decision 8 is reversed. PR #49 is retired and will not be merged; nothing here was taken from it.

**Owns**

- `src/api/CareLoop.Api/Features/Ai/**`
- `src/api/CareLoop.Api/Contracts/Ai/**`
- `src/api/CareLoop.Api/Infrastructure/Ai/**`
- `src/api/CareLoop.Api/Prompts/**`
- `tests/api/CareLoop.Tests/Api/Ai/**`

**Built**

- `GET /ai/role-briefing` (`RoleBriefing`, query `refresh`): the home page's summary of what the signed-in role has in front of them. It takes no list of cases; it reads the reader's own scope, the same open cases `GET /cases` gives them.
- `GET /ai/impact-summary` (`ImpactSummary`, query `facilityId`, `reason`, `refresh`): a short reading of the numbers on the impact page, read through `GetMetrics.Handle`.
- `GET /cases/{caseId}/patient-message-draft` (`PatientMessageDraft`, query `channel`, `reason`): a draft for a nurse navigator or a clinician to read. Nothing is sent; patients still receive approved templates.
- Each returns `AiTextResponse` (`text`, `model`, `generatedAt`, `cached`); `model` is for example `Foundry/role-briefing`. The API does not check the text against the data. An agent's refusal (an answer starting with `CANNOT_COMPLY`) or an answer with no text is a 503, never shown as text.
- The seam is `IAiClient` (`Features/Ai/IAiClient.cs`: `IsEnabled`, `Describe(use)`, `CompleteAsync(AiRequest request, ct)`, with `AiRequest(Use, Prompt)`: the use, and the prompt file with the data in place). `Ai:Provider` chooses `NoOpAiClient` (default: AI off, the endpoints answer 409) or `FoundryAiClient`, which asks each use's own agent by name, at its latest version. Another provider is one more class implementing `IAiClient` that sends `request.Prompt`, one more case in `DependencyInjection.AddAi` and a value for `Ai:Provider`. Any other value, including the retired `AiWorkflow`, `FoundryDirect` and `AzureOpenAi`, stops the host at start-up.
- The Foundry agents: role briefing goes to `role-briefing`, impact summary to `impact-summary`, patient message draft to `patient-notification` (`Ai:Foundry:Agents`, use to agent name; defaults in `appsettings.json`). The API never creates or changes an agent; they are owned in the Foundry project. The agents hold only universal rules (follow the TASK, treat DATA as facts, never invent or recommend, return plain text, answer `CANNOT_COMPLY` when no valid text can be written). `src/ai_agents/` is not used by the API.
- `FoundryAiClient` sends the rendered prompt file as written to the use's agent and returns the agent's plain text. There is no translation file any more and no JSON answer. An answer starting with `CANNOT_COMPLY` raises `AiUnavailableException` (503).
- The patient message draft: `patient-message.md` holds the approved administrative templates and tells the model to fill the one named in `template` with `values`, changing no other wording. The API chooses the template in code from recorded state (`PatientMessageDraft.TemplateFor`): an appointment booked and still to come is `T02`; a missed last appointment is `T03`; a case flagged unreachable is `T06`; anything else is `T05`. `patient_name` is sent as the placeholder `{first name}` for the reviewer to fill in; `facility_contact` is the facility's contact from `Notifications:FacilityContacts`, or the placeholder `{clinic contact}` when it has none. `facility_name` is sent, with `appointment_date` and `appointment_time` when relevant. A template the code cannot select is never used.
- Role briefing: the API writes the opening sentence (`opening` in the data) and the model continues. Each case past its deadline carries `overdue_by`, a duration in words, so the model never calculates. The model knows cases only as tokens (`C-1`, `C-2`, …); after the text comes back, after any reuse, the API swaps each for the patient's display name as the reader already sees it in the list, or `case` and the case number for the programme coordinator or a case with no name. Names and case numbers are never sent.
- Impact data: each of `shares` is `numerator`, `denominator`, `percent`; a withheld value is the word `withheld` (`ImpactSummary.SmallCount`). The prompt asks for three short paragraphs (timeliness, case progression, measurement limitations). While `Ai:SyntheticData` is `true` (the default) the API, not the model, ends the summary with "Simulated on synthetic data; not evidence of real-world improvement."
- No key: `DefaultAzureCredential` (the `apps` managed identity in Azure, `az login` locally). This added the NuGet package `Azure.Identity` 1.21.0, approved by the project owner.
- `Prompts/`: one plain-text file per use (`role-briefing.md`, `impact-summary.md`, `patient-message.md`) is a `TASK:` followed by a `DATA:` section with one `{{data}}` marker where the API puts the data. The files apply on Foundry for all three uses. Edit and restart; a missing file, or one without exactly one `{{data}}`, stops the host (`AiPrompts`).
- Reuse (`AiText`): a role briefing or impact summary is kept in memory under the provider and the exact prompt (`Ai:CacheHours`, 6 by default, `0` switches it off; `?refresh=true` asks again). The patient message draft is never reused.
- Read audit: the normal read audit runs on every request, reused answer or not. The views are `role-briefing`, `metrics` (the impact summary) and `patient-message-draft` or `patient-message-draft-denied`. `CaseReads` has no new columns. The migration `RemoveHandoffSummaries` only drops the `HandoffSummaries` table.
- Tests: `tests/api/CareLoop.Tests/Api/Ai/AiEndpointsTests.cs` and `AiProviderTests.cs` (the prompt files' and the Foundry client's tests). The opt-in live test `Live_the_endpoints_get_text_from_their_foundry_agents` runs only when `CARELOOP_LIVE_FOUNDRY_ENDPOINT` is set. Run on 19 November 2026 with the agents' latest versions (`role-briefing` 8, `impact-summary` 7, `patient-notification` 7): all three uses answered 200 from their own agents.
- On the dashboard, the worklist shows the role briefing under its greeting (`src/web/src/features/worklist/RoleBriefing.tsx`), with a Refresh action. The impact page shows the impact summary under its heading (`features/impact/ImpactSummary.tsx`), and the case page has a card where a nurse navigator or clinician asks for a patient message draft (`features/case/PatientMessageCard.tsx`); nothing is sent from it.

**Removed**

`IAiProvider`, `NoOpAiProvider`, `FoundryDirectProvider`, `Features/Summaries/**`, `Contracts/Summaries/**`, `Infrastructure/Persistence/Summaries/**`, `HandoffSummaryRecordConfiguration`, `HandoffSummaryTests`, the setting `Ai:AutoDraftOnHandoff`, and the routes `POST /cases/{caseId}/handoff-summary`, `…/approval` and `…/rejection`. The FHIR publisher no longer sends a `DocumentReference`. The HIS simulator's own handoff-document inbox (`src/simulators`) is untouched.

**Files of other workstreams this change touched (19 November 2026)**

| Owner | Files | What changed |
|---|---|---|
| Shared | `Program.cs`, `Infrastructure/DependencyInjection.cs`, `CareLoop.Api.csproj`, `appsettings.json` | Routes and `AddAi`; `Azure.Identity`; the `Ai` section |
| Shared | `docker-compose.yml`, `.env.example`, `.github/workflows/deploy.yml` | AI settings; the stale `Ai:AzureOpenAi` section and API key settings are gone |
| WS2 | `CareLoopDbContext`, migration `RemoveHandoffSummaries` and the model snapshot | `HandoffSummaries` dropped |
| WS1 | `CaseEventType`, `SourceEvent`, `TransitionRules`; `HandoffSummaryApproved` removed | Event type 101 is retired and must not be reused |
| WS3 | `FhirPublisher`; `HandoffDocumentReferenceMapper` removed | No `DocumentReference` is published |
| WS4 | A comment in `RecipientResolver` | It no longer names `HandoffSummaryApproved` |
| WS6 | A removed `using` in `RunSimulation`, `contracts/openapi/careloop.json`, `src/web/**` | The three AI routes replace the handoff summary routes in the OpenAPI document; the summary screen is removed from `src/web` (the dashboard shows the three AI texts). `GetMetrics` is unchanged: the impact summary calls its handler |
| Infra | `infra/azure/**` | `apiAiProvider` is `NoOp` or `Foundry`; the API is given only the project endpoint (`Ai__Foundry__ProjectEndpoint`); the Foundry User role for the API's identity. A Foundry created with `CREATE_FOUNDRY=true` has no agents |

Open contract points for WS9 are in the table below.

**Scope boundary:** the backend owns workflow state, ownership, deadlines, escalations, and metric calculation. AI writes draft text for staff only; it does not coordinate stages, calculate metrics, mutate cases, or send messages, and it never discloses a result. [`ai-design/integration.md`](ai-design/integration.md) is the contract: what AI may do, what it is sent, how an answer is checked, reused and audited, and what happens when it fails. [`ai-design/ai-spec.md`](ai-design/ai-spec.md) is the earlier design proposal, superseded where the two differ; its handoff summary, Arabic translation, closure review pack and reply triage are not built.

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
- C10 (Done, edited by WS8; to remove under C22): a `rate-confidence` patient behaviour in `scenario.schema.json`, plus an optional `confidence` (1 to 5) beside another response. No scenario file uses it.
- C13: a scenario with a consented caregiver and one where consent is withdrawn. The schema's step `action` list has no consent action yet: add one (for example `consent`, with grant or withdraw) for the HIS simulator to send, agreed with WS7. The consented-caregiver scenario books an appointment with a place and expects the caregiver's Email message (date, time and place only); the withdrawn one books after withdrawal and expects no caregiver message.
- Decision 2: there is no appointment-confirmation behaviour.
- C22 (decisions 21 to 30), to do with WS10, agreed with WS7 and WS8 (both mirror the schema; the HIS loader refuses unknown fields):
  - Add a `contact` to scenario patients, and to the consent action's caregiver: `{ "channel": "sms" | "email", "address": "SYN-…" }`. WS7 sends it as the `telecom`, replacing `Contacts:PatientChannel` and the synthetic id it sends today. WS8 finds the behaviour by that address.
  - Remove `understand` and `rate-confidence` from `patientBehaviour.response`, and `confidence`; a patient can only reply 1 (`acknowledge`) or 2 (`call-me`), or stay silent. The messaging simulator already refuses them; the HIS loader still accepts `understand`.
  - Nurse-recorded outcomes (teach-back, rating, "will come" on a call) become steps against `POST /cases/{caseId}/call-outcome`, not patient behaviours. C8's "understand after a reminder" scenario becomes "the patient acknowledges on the reminder, then the nurse records understanding".
  - New scenarios: a patient reached by email, a patient with both a phone number and an email, and a reply that is neither 1 nor 2 (expect `CallRequested` and no acknowledgement).
  - The consented-caregiver scenario (C13) expects the caregiver's email in the messaging simulator.
  - Journey tests assert the SMS or email attempts on the notification track, and that staff notices reach the configured staff addresses.
- Review of PR 43 (decisions 32 to 36), to do with WS10:
  - A call-back scenario expects the call-back email at the nurse navigators' address, and the case to move on once the nurse records the call.
  - Booking an appointment expects a patient message with the date, time and place (local time), as well as the caregiver's.
  - Scenario facilities: the scenarios use `SYN-FAC-A`, which no one in "Signed in as" belongs to; use a facility the dashboard knows (`family-health-centre`) or add it to the sign-in list (WS7, WS10).

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
| `PUT` route shape | WS3, WS7, WS8 | Settled: WS3 and WS7 implement `PUT /fhir/{resourceType}/{id}`; URL and body ids must match or the request is refused with 400. CareLoop's outbound sends use the same shape. WS8 sends no `PUT`: it posts `Communication`s to the notification callback and reads `GET /fhir/Task/{id}`. `AGENTS.md` is aligned. |
| `Communication.category` codes | WS4, WS8 | Retired (decision 22, C22): no `Communication` crosses between CareLoop and the simulator. A delivery report is `{ "type": "delivery", "status": "delivered" \| "failed" }`, a reply `{ "type": "reply", "text": … }`. |
| External system identifiers | WS2, WS3, WS7 | Settled and built: WS7 sends `meta.source = his-simulator`, matching the development `ExternalSystems:Systems` key. WS3 refuses missing source with 400 and unconfigured source with 403. The case retains that source system and replies are routed to it. |
| Scenario patient behaviour | WS7, WS8, WS10 | A scenario's steps are hospital-system events only. How a patient reacts is `patients[].behaviour` (`delivery`, `response`, `respondAfter`, `respondToNotification`), applied by the messaging simulator when a message arrives at the patient's address. Since C22 `response` is `none`, `acknowledge` (replies 1) or `call-me` (replies 2); `understand`, `rate-confidence` and `confidence` are refused and are to be removed from the schema (WS10). `respondToNotification` counts the notifications about one case received since the behaviour was put in force. Cohort runs need a way to give each generated patient their scenario's behaviour. |
| Time in a live run | WS1, WS5, WS7, WS8, WS10 | CareLoop runs on real time. A live run uses the demo pathway folder (`Pathways:Directory`), whose deadlines are divided by 1800, and both simulators pace scenarios with `Scenarios:RealSecondsPerScenarioHour` = 2. The three figures must stay equal. End-to-end tests should assert outcomes, not exact times, and leave headroom for slow machines. |
| Simulator provider wire format | WS4, WS8 | Settled under C22 (decision 22): `POST {Notifications:<Sms\|Email>:BaseUrl}/messages` with `{ messageId, channel, to, subject, body, language, metadata: { caseId, audience } }`; `messageId` is the notification attempt identifier, and a resend repeats it. The simulator calls back on `POST /notifications/inbound/sms` or `/email` with a delivery report (`messageId`, `status`, `reason`, `at`) or a reply (`replyId`, `inReplyTo`, `from`, `text`, `at`), each returning `metadata`. Every callback carries `X-CareLoop-Signature: sha256=` plus the lower-case hex HMAC-SHA256 of the raw UTF-8 body, keyed with `SIMULATOR_INBOUND_SECRET` (`Notifications:<channel>:InboundSecret` in the API, `CareLoop:InboundSecret` in the simulator); anything unsigned or wrongly signed gets 401. |
| Patient replies beyond acknowledgement | WS1, WS4, WS6, WS8 | Settled (decisions 2, 3, 23, 24, 29): a patient replies `1` (acknowledgement) or `2` (call-me) by text or email, and any other reply is a call-me. Teach-back and the confidence rating are recorded by a nurse navigator after a call. A patient reply never changes appointment status. |
| Simulated marker on FHIR resources | WS7 | Settled and built: WS7 marks the resources it sends with `meta.tag` in `https://careloop.invalid/fhir/CodeSystem/data-origin`, code `simulated`, matching the API samples. CareLoop neither requires nor emits a marker. Since C22 WS8 sends no FHIR; its messages and callbacks carry `simulated` in their own JSON. |
| Staff notification transport | WS4, WS7, WS8 | Settled under C22 (decisions 21, 27, 28): a staff notice is an email or text message to the address configured for the owner's role (`Notifications:StaffContacts`), shown in the messaging simulator's staff inbox. The `staff-notice` Task and its `PUT` to the HIS simulator are retired. |
| Urgency on the review `Task` | WS3, WS7 | Settled and built: WS7 carries source-reviewed urgency unchanged in `https://careloop.invalid/fhir/StructureDefinition/reviewed-urgency` as `valueCode`, because placeholder codes do not fit FHIR priority. WS3 reads the extension first and retains its existing `Task.priority` fallback. |
| Nine event types | WS2, WS3 | Delivery reports and patient responses share one event, `NotificationUpdated`. Caregiver consent is its own event, `ConsentRecorded` (C13), so there are exactly nine source events and handlers. |
| Internal events | WS2, WS5, WS6 | `DeadlineElapsed` and `EscalationAcknowledged` are saved through `ICaseRepository.SaveAsync` directly, not through a handler. |
| Contact point channel | WS3, WS4, WS7, WS8 | Settled under C22 (decision 21): `ContactPoint.system = sms` is a text message and `email` an email; every other system maps to nothing. WS7 sends `sms` for patients (or `email`, by configuration) and `email` for caregivers. Staff contacts are configuration, not FHIR. |
| Publishing the approved summary | WS1, WS3, WS9 | No longer applies (19 November 2026): the handoff summary is removed. `HandoffSummaryApproved` is gone, event type 101 is retired, and `FhirPublisher` sends no `DocumentReference`. `HandoffProposed` still sends the case `Task`. |
| Repeated requests | WS2, WS3 | Settled with this review: the HIS sends a consultation or referral as a `ServiceRequest` plus a `Task`, and resends new versions. A request repeated while the consultation is already requested, or the referral already sent, is recorded but leaves the deadline alone; a request after a rejection restarts it. |
| Rejected events | WS1, WS2 | A refused event is saved with `Disposition = Rejected`, so the attempt is audited through the same write path. |
| Stage model | WS1, WS6 | Settled (decision 1): five stages in the Domain; the design's ten stages are display labels derived from stage plus track statuses, by the API since decision 19. |
| Patient appointment confirmation | WS1, WS4, WS8 | Settled (decision 2): dropped. "I will come" is an acknowledgement; a patient reply never changes appointment status. |
| Confidence rating | WS1, WS2, WS3, WS4, WS6, WS8, WS10 | Settled (decision 3, source changed by decisions 24 and 30): `NotificationUpdateKind.ConfidenceRated` with a 1 to 5 value, recorded by a nurse navigator after a call through `POST /cases/{caseId}/call-outcome`. No reply is a rating. |
| Where the AI summary is approved | WS6, WS7, WS9 | No longer applies (19 November 2026): the handoff summary and its screen are removed, and CareLoop sends the HIS simulator no `DocumentReference`. |
| Where the model is called | WS9 | Settled: the API asks the agents in a Microsoft Foundry project directly, one per text, through `IAiClient` (decision 8 reversed, 19 November 2026). The Python AI workflow service is retired. `Ai:Provider` is `NoOp` (default) or `Foundry`. |
| Clinical text in a prompt | WS2, WS9 | Decision 9 was written for the handoff summary, which is removed. Today no prompt carries a result, a name, an identifier, a contact detail or free text from the hospital system; what each of the three prompts carries and withholds is listed in `ai-design/integration.md`. |
| Draft shape and reviewer decision | WS6, WS9 | No longer applies (19 November 2026): decision 10 described the stored handoff summary. The three AI texts are not stored and have no approval step; each request is recorded in the normal read audit. |
| When a draft is produced | WS9 | No longer applies (19 November 2026): decision 11 and `Ai:AutoDraftOnHandoff` went with the handoff summary. An AI text is written only when a page asks for it. |
| Summary approver roles | WS1, WS9 | Closed (19 November 2026): no longer needed, the handoff summary is removed. |
| AI settings in the shared config files | WS9, plus whoever owns the file | Closed (19 November 2026): the `Ai:AzureOpenAi` section and the API key settings are gone from `appsettings.json`, `.env.example` and `docker-compose.yml`. The settings are `Ai:Provider`, `Ai:TimeoutSeconds`, `Ai:CacheHours`, `Ai:PromptsDirectory`, `Ai:SyntheticData`, `Ai:Foundry:ProjectEndpoint` and `Ai:Foundry:Agents`. There is no model setting any more: the model deployment name is gone from Docker Compose, `.env.example`, `infra/azure/**` and the deploy workflow. |
| AI text without an approval step | WS6, WS9 | Open. Staff-only AI text is shown with a label and no approval step today; record the decision. |
| Sending an AI-drafted patient message | WS4, WS9 | Open. Today the draft is a preview and patients receive approved templates. Sending one would need a person's approval of the exact text, recorded as an event. |
| Who may ask for a patient message draft | WS1, WS9 | Open. Today a nurse navigator or a clinician (`PatientMessageDraft.AllowedRoles`). |
| Small numbers in the impact summary | WS6, WS9 | Open. The small-number threshold (5, the placeholder `ImpactSummary.SmallCount`). |
| AI text is not checked | WS6, WS9 | Open. The model's text is not checked against the data; only a refusal or an answer with no text is caught (503). If a wrong summary is ever seen, add a check for that case rather than a general parser. |
| Patient message templates | WS9, the agents' owner, the clinical lead | Open. The draft works: the API selects one of four approved administrative templates in `patient-message.md` from recorded state (`T02` appointment reminder, `T03` missed appointment follow-up, `T06` unsuccessful contact follow-up, otherwise `T05` care-team callback request). To decide: which further templates the API should be able to select and the recorded state each would need; and the real clinic contact numbers, which are a setting (`Notifications:FacilityContacts`) holding synthetic values for now. |
| Web search tool on the `patient-notification` agent | WS9, the agent's owner | Open. The agent has a web search tool attached. A patient message draft needs no web search; raise it with the agent's owner. |
| Agents are called at their latest version | WS9, the agents' owner | Open. A change to an agent's permanent instructions can change behaviour. Agree how changes are announced. |
| Python sample scripts | WS9 | Open. `src/ai_agents/` holds sample scripts the API does not use; decide whether to keep them. |
| Roles | WS1, WS6, WS7 | Settled (decision 5): add `nurse-navigator`, `receiving-clinician` and `lab` (C11). `DemoRoles` is shared; WS6 edits it after announcing. |
| Consultation reply deadline | WS1, WS2 | Settled (decision 6): `answer-consultation` and `respond-to-referral` replace `respond-to-handoff`; both placeholders (C12). |
| Caregiver and consent | WS1, WS2, WS4, WS8 | Settled (decision 7): build it (C13). `RecipientKind.Caregiver`, a `Consent` record on the case, caregiver gets date, time and place only, shown in the messaging simulator; since C22 the caregiver's contact is an email address and real email delivery stays out of scope. |
| Caregiver appointment message | WS1, WS2, WS3, WS4, WS7 | Settled and built: domain event `AppointmentScheduled` (`ScheduledFor`, `Location`) is raised on booking, rebooking and time changes; WS7 sends the Location participant and WS3 reads its `actor.display`. WS4 notifies the patient, and the caregiver only while consent is active (decision 33); times are local; `FhirPublisher` ignores the event. A booking without a time raises nothing. |
| Acknowledgement in escalation notices | WS1, WS4, WS5 | Settled and built (WS1, WS4): `EscalationRaised.AcknowledgedByRole` and `AcknowledgedAt`, filled by `Case.Escalate` from the previous level, so WS5 only calls `Escalate`; WS4's staff templates show them through `{{previousAcknowledgement}}`. |
| Escalation identifier | WS1, WS2, WS6 | Settled and built (WS1, WS2): `Escalation.Id` (uuid), carried on `EscalationRaised.EscalationId`, persisted unique; `Case.AcknowledgeEscalation(escalationId, ...)` refuses a superseded escalation. WS6's acknowledgement endpoint takes it as `{escalationId}`. |
| Read audit | WS2, WS6 | Settled and built (WS2): staff reads are recorded in `CaseReads` (`CaseReadRecord`), not in `AuditEvents`, because a read changes no case state and has no source event. WS6 writes one per read (C4, C19). |
| Escalation acknowledgement | WS1, WS2, WS5, WS6 | Settled (decision 13): C15. Only the role the escalation went to may acknowledge; it is audited and never clears an exception, changes the owner or stops the clock. |
| Patient identifiers | WS1, WS2, WS3, WS6, WS7 | Settled (decision 15): C20. The screening result carries the patient's display name and MRN; the case stores them; staff roles read them, `coordinator` does not. `AGENTS.md` changes (shared; agree first). |
| Display labels | WS1, WS6, WS10 | Settled (decision 19): the ten labels are computed by a Domain function and returned on reads and in metrics; the web app never recomputes them. |
| Evidence kinds | WS1, WS2, WS3, WS6, WS7, WS10 | Settled (decision 20): C21. The pathway declares the kinds (placeholders); intake rejects others; a `treatment-plan` outcome starts the `start-treatment` deadline. The PM confirms the names; WS3 and WS7 agree the FHIR code mapping. |
| Reply deadline placeholders | WS1 | Settled (decision 18): the pathway file follows screen 4 of the design. Clock time, not working hours; a working-hours calendar is future work. |
| HIS simulator screens | WS7, WS10 | WS7 built the four screens and API-driven headless replay (decision 16, C6). The full headless patient journey demo remains WS10. |
| Patient addresses in scenarios | WS7, WS8, WS10 | Open (C22): scenario files carry no phone number or email address. Until WS10 adds a `contact`, WS7 sends each patient's synthetic id as the address, and WS8 finds the behaviour by it. |

## Design gap tracker

One row per item and workstream, so no part is lost between owners. **Open** means not started or not merged;
**Done** means built, tested and merged on `main` (or on this branch's pull request). Change a row only in the change
that finishes it. Items come from [`ui-design-gap-analysis.md`](ui-design-gap-analysis.md); C14 and C16 to C18 were
withdrawn. C22 retired some earlier parts; those rows stay Done and say so.

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
| C7 SMS reply codes | WS8 | `1` acknowledge, `2` call-me on the `Sms` inbox (read by CareLoop since C22) | Done |
| C8 Exception-path and demo scenarios | WS10 | Six exception scenarios with `expect` entries; demo scenario for screens 15, 16, 19 to 21 | Open |
| C8 | WS10 | Replace `PLACEHOLDER-EVIDENCE-KIND` in `happy-path.json` with a declared kind (C21) | Open |
| C9 Dashboard | WS6 | Screens 1 to 4, 17, 18; escalation acknowledge; other-cases card. The handoff summary screen (draft, edit-on-approve, reject) was built and then removed on 19 November 2026 | Done |
| C10 Confidence rating | WS1, WS2, WS3 | Domain, payload, handler, `StatusMap` | Done |
| C10 | WS4 | `SimulatorInboundNotificationHandler` maps `confidence-rating` (retired by C22) | Done |
| C10 | WS6 | `confidenceRate` on `MetricsResponse` | Done |
| C10 | WS8 | Inbox rating action and `confidence-rating` `Communication` builder (removed by C22) | Done |
| C10 | WS10 | `rate-confidence` behaviour in `scenario.schema.json` (edited by WS8) | Done |
| C11 Roles | WS1 | Pathway `allowedRoles`, `authorizedRoles`, `OwnershipRules` | Done |
| C11 | WS6 (shared) | `nurse-navigator`, `receiving-clinician`, `lab` in `DemoRoles.HumanRoles` | Done |
| C11 | WS7 | `ActingStaff` roles and clinician actions page | Done |
| C12 Per-kind reply deadlines | WS1, WS2 | Pathway deadlines and next actions | Done |
| C12 | WS5 | Ordinary deadlines in `ComputeDueAt`, clock time | Done |
| C13 Caregiver and consent | WS1, WS2, WS3 | `Consent`, `ConsentRecorded`, `ConsentMapper`; `AppointmentScheduled`, `Location` on payload and mapper | Done |
| C13 | WS4 | Route `AppointmentScheduled` to a consented caregiver; caregiver templates (standard and SMS-safe); consent checked at send | Done |
| C13 | WS7 | `Consent` builder and `HisEventKind`; `Location` participant on `AppointmentBuilder` | Done |
| C13 | WS8 | `Email` in `InboxChannel`; recognise caregiver messages | Done |
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
| C22 Text message and email only | WS1, WS2 | `NotificationChannel` is `Sms` and `Email`; optional payload channel; migration `SmsAndEmailChannelsOnly` | Done |
| C22 | WS3 | `sms` and `email` contact points only; `Communication` mapping and samples removed | Done |
| C22 | WS4 | SMS and email providers and inbound handlers over plain JSON; reply rules; staff contacts; templates | Done |
| C22 | WS6 | `POST /cases/{caseId}/call-outcome`; "After a call with the patient" card; timeline source wording | Done |
| C22 | WS7 | `sms`/`email` contact points; staff-notice and `CommunicationRequest` receipt removed | Done |
| C22 | WS8 | Messaging simulator: `POST /messages`, JSON callbacks, four inboxes, replies as typed; app, portal, case status, teach-back and rating removed | Done |
| C22 | Shared | `appsettings`, `docker-compose.yml`, `.env.example`, `AGENTS.md`, `README.md` | Done |
| C22 | WS10 | Scenario `contact`; remove `understand`, `rate-confidence`, `confidence`; email, both-channel and unrecognised-reply scenarios; call outcomes as steps | Open |
| Decision 19 Display labels | WS1 | `CaseDisplayLabels.From` | Done |
| Decision 19 | WS6 | `DisplayLabel` on case reads and in metrics | Done |
| Decision 19 | WS10 | Journey tests assert the label at each happy-path step | Open |
| Summary approver roles | WS1, WS9 | `summaryApproverRoles` in the pathway | Dropped (the handoff summary is removed) |

