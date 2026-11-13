# UI design gap analysis

A comparison of the thirteen screens in [ui-design/README.md](ui-design/README.md) with the API contracts, the Domain, the two simulators and the scenario files, as of 13 November 2026. Its purpose is to say, for each difference, who closes it: the codebase (by workstream) or the product manager (in the design). The seven decisions the comparison raised were taken by the product owner on 13 November 2026 and are recorded in section 5.

The comparison is against contracts and stubs, not running behaviour. Every API route except `/health` still returns `501 Not Implemented`, and both simulators are scaffolds. Nothing here changes the rules in [AGENTS.md](../AGENTS.md).

## Summary

- The screens and the code describe the same product with different vocabularies. The screens use ten stages and stage-entry events; the Domain has five stages, nine source event types (eight at the time of the comparison; `ConsentRecorded` was added for C13) and four tracks. Decided: five stages stay in code and the ten are display labels.
- Several screens need data or endpoints the code has no seam for: a programmes read model, a confidence rating, caregiver consent, more roles, per-kind reply deadlines, team-scoped reads, source system on the timeline, and richer metrics. All are now codebase items (C1 to C13).
- The HIS simulator's clinician screen can record four actions. Screens 5 and 6 need four more.
- The scenarios never exercise the exception paths that screens 5, 7, 8 and 13 show.

## Section 1: Where the design and the code agree

| Screen | Agreement |
|---|---|
| 1 Worklist | The six filter chips are the six values of `CaseExceptions` (`Overdue`, `Rejected`, `Unreachable`, `Unacknowledged`, `Unassigned`, plus urgent from `Urgency`). `CaseSummaryResponse` and `ExceptionItemResponse` carry one next action, one owner and one due time, as the design rule requires. |
| 2 Case | `CaseDetailResponse` has stage, urgency, owner, proposed owner, next action, due time, escalation flag, exceptions, the four tracks and closure reason. `GetTimeline` gives the audit trail. |
| 7 Accept or decline | `ReferralUpdatePayload` carries `Status` (`Sent`, `Accepted`, `Rejected`), `ReceivingFacilityId` and a free-text `Reason`. The sender stays owner until acceptance (`OwnershipRules`). |
| 8 Close the case | The six closure reasons on screen are the six values of `ClosureReason`. The pathway's `closureRules` encode the per-reason evidence (`requiresEvidenceReference`, `requiresExceptionNote`, `requiresNextSurveillanceDate`, `minimumDocumentedAttempts`), and `ClosureGate` blocks the rest. |
| 9, 10, 11 Patient responses | Delivered, failed, acknowledged, understood (teach-back) and call requested all exist as `NotificationUpdateKind`, and the patient-channel simulator has inbox actions for acknowledge, understand and call-me. Acknowledgement and understanding are recorded separately, as screen 10 asks. |
| 13 Empty and error states | Duplicate results dedupe on `(SourceSystem, SourceEventId)`; closure is gated; a failed contact raises `Unreachable` with the pathway's `contact-patient-another-way` next action; AI is a draft until approved (`HandoffSummaryStatus`). |

## Section 2: Vocabulary mismatches

| Design says | Code has | Resolution |
|---|---|---|
| Ten stages, `RESULT_RECEIVED` to `CLOSED` | Five stages in `CaseStage`: `Received`, `ReviewPending`, `FollowUpActive`, `ClosureReview`, `Closed`, with four tracks (notification, consultation, referral, appointment) carrying their own statuses | Decided: five in code, ten as display labels (C9) |
| Stage-entry events: `ResultReceived`, `CaseCreated`, `OwnerAssigned`, `PatientNotified`, `PatientAcknowledged`, `ConsultationRequested`, `FollowUpStarted`, `OutcomeDocumented`, `ClosureReviewRequested`, `ClosureAuthorised` | Eight source event types in `CaseEventType`: `ScreeningResultReceived`, `ClinicalReviewCompleted`, `NotificationUpdated`, `ConsultationUpdated`, `ReferralUpdated`, `AppointmentUpdated`, `OutcomeRecorded`, `ClosureDecided`; plus internal `DeadlineElapsed` and `HandoffSummaryApproved` | PM renames |
| `ConsultationRequested`, `ConsultationAnswered`, `CallbackRequested` | `ConsultationUpdated` with status `Requested`, `OwnerProposed`, `Accepted`, `Rejected`, `OutcomeRecorded`; a patient call request is `NotificationUpdated` with kind `CallRequested` | PM renames |
| `ReferralSent`, `ReferralAccepted`, `ReferralRejected` | `ReferralUpdated` with status `Sent`, `Accepted`, `Rejected` | PM renames |
| `AppointmentScheduled`, `AppointmentConfirmed` | `AppointmentUpdated` with status `Scheduled`, `Attended`, `Missed`; no patient-side confirmation | PM renames; `AppointmentConfirmed` is dropped (decision 2) |
| `ClosureAuthorised`, `EvidenceAttached` | `ClosureDecided`, `OutcomeRecorded` | PM renames |
| `TeachBackSubmitted`, `ConfidenceRated` | `NotificationUpdated` with kind `Understood`; no confidence rating | PM renames the first; the second is built (C10) |
| `SummaryApproved` | `HandoffSummaryApproved`, raised by `POST /cases/{caseId}/handoff-summary/{summaryId}/approval` in CareLoop, not in the HIS | PM renames; approval moves to the CareLoop web app (decision 4) |
| `GET /programmes` | No endpoint; the pathway file is read internally by `JsonPathwayProvider` | Codebase, WS6 |
| Eight personas: patient, family member, nurse navigator, screening doctor, receiving doctor, lab team, facility lead, programme coordinator | Three human roles in `DemoRoles`: `clinician`, `coordinator`, `facility-lead`, plus `system` | Codebase, C11: add `nurse-navigator`, `receiving-clinician`, `lab` |

The design README cites [architecture.md](architecture.md) as the source of the ten stages, and [contracts/README.md](../contracts/README.md) names the same stage-entry events. Both documents therefore disagree with the Domain as built in WS1. Each now carries a note recording decision 1.

## Section 3: Codebase updates

Additive changes. None alters the Domain's transition rules or contradicts `AGENTS.md`. Each is also listed under its workstream in [workstreams.md](workstreams.md).

| # | Gap | Change | Files | WS |
|---|---|---|---|---|
| C1 | Screen 4 has no read model | Add `GET /programmes` and `GET /programmes/{pathwayId}` returning the pathway definition: stages, tracks, deadlines with `clinicallyApproved`, escalation ladder, closure rules, version and `isPlaceholder`. Add `ProgrammeResponse` and friends to `Contracts/`. OpenAPI regenerates on build. | `src/api/CareLoop.Api/Features/Programmes/` (new), `Contracts/Programmes/` (new) | WS6 |
| C2 | Pathway lacks what screen 4 shows | Add optional `citation` to each `DeadlineDefinition` and `ClosureEvidenceRule`, a `changeLog` list (version, date, approver, summary) on `PathwayDefinition`, and an `ownerRole` per next action. Keep every value a placeholder until approved. Keep `demo/breast-screening.json` equal apart from durations. | `src/api/CareLoop.Domain/Pathways/*.cs`, `Infrastructure/Pathways/breast-screening.json`, `demo/breast-screening.json` | WS1 (follow-up) |
| C3 | Timeline entries do not name the source system | Add `SourceSystem` to `TimelineEntryResponse`, populated from the source event. | `Contracts/Cases/TimelineEntryResponse.cs`, `Features/Cases/GetTimeline.cs` | WS6 |
| C4 | Reads are not scoped to a team | Add `facilityId` to `GET /cases`; refuse `GET /cases/{id}` and `/timeline` when the caller's facility (from the demo headers) is not the case's facility, unless the caller is `coordinator`; write an `AuditEvent` for every case read. Add `X-Demo-Facility` to `DemoRoles`. | `Features/Cases/*.cs`, `Infrastructure/Auth/DemoRoles.cs` (shared; agree first) | WS6 |
| C5 | Metrics too thin for screen 3 | Extend `MetricsResponse` with per-transition medians and denominators (result to notification, to acknowledgement, to referral accepted, to appointment attended, to outcome, to closure), an understanding rate, and closures by reason. Give `SimulationArmResponse` the same shape so "escalation off" compares like for like. | `Contracts/Metrics/*.cs`, `Features/Metrics/GetMetrics.cs`, `RunSimulation.cs` | WS6, WS10 |
| C6 | HIS clinician screen cannot send a consultation, referral or appointment | Add `POST /actions/consultation-request`, `/actions/consultation-answer`, `/actions/referral`, `/actions/appointment`, each wired to the existing builder, and the matching forms on the clinician actions page. | `src/simulators/his/HisSimulator/Outbound/ClinicianActionEndpoints.cs`, `ClientApp/src/features/clinician-actions/` | WS7 |
| C7 | Screen 9 assumes SMS reply codes | On the `Sms` inbox channel, accept an inbound reply body of `1` as acknowledge and `2` as call-me, and record it as a reaction. | `src/simulators/patient-channel/PatientChannelSimulator/Inbound/` | WS8 |
| C8 | Scenarios skip the exception paths | Add scenarios for: call-me then nurse call-back; understand after a reminder; consultation rejected; missed appointment then rebook; closure `Declined`; closure `Untraceable` with documented attempts. Add `expect` entries for each. | `src/simulators/scenarios/` | WS10 |
| C9 | Web dashboard is a starter | Build screens 1 to 4 against `GET /cases`, `/exceptions`, `/cases/{id}`, `/cases/{id}/timeline`, `/metrics` and the new `/programmes`. Read-only, except the AI summary draft and approve buttons on the case screen (decision 4). Derive the ten display labels of the flow from stage plus track statuses (decision 1). | `src/web/` | WS6 |
| C10 | Confidence rating (decision 3) | Add `NotificationUpdateKind.ConfidenceRated` carrying a 1 to 5 value; store `Confidence` on `NotificationTrack`; handle it in `NotificationUpdateHandler`; map a `Communication` category `confidence-rating` in `StatusMap` and `SimulatorInboundNotificationHandler`; add a `confidenceRate` metric; add an inbox action and `Communication` builder in the patient-channel simulator; add a scenario behaviour `rate-confidence`. | `Domain/Tracks/NotificationTrack.cs`, `Contracts/Events/Payloads/NotificationUpdate*.cs`, `Features/Events/Handlers/NotificationUpdateHandler.cs`, `Features/Fhir/StatusMap.cs`, `Infrastructure/Notifications/SimulatorInboundNotificationHandler.cs`, `Contracts/Metrics/`, patient-channel `Inbound/`, `Outbound/`, `scenario.schema.json` | WS1, WS2, WS3, WS4, WS6, WS8, WS10 |
| C11 | Roles (decision 5) | Add `nurse-navigator`, `receiving-clinician` and `lab` to `DemoRoles.HumanRoles`; add them to the pathway's `allowedRoles` and `closureRules.authorizedRoles` where the design gives them the action (nurse records acknowledgement and understanding; receiving clinician accepts, declines, books, records outcome and closes; lab accepts a request and records the test); update `OwnershipRules`; let the HIS simulator act as each role. | `Infrastructure/Auth/DemoRoles.cs` (shared; agree first), `Infrastructure/Pathways/*.json`, `Domain/Rules/OwnershipRules.cs`, HIS `Outbound/ActingStaff.cs` and clinician actions page | WS1, WS6, WS7 |
| C12 | Per-kind reply deadlines (decision 6) | Replace the single `respond-to-handoff` deadline with `answer-consultation` and `respond-to-referral`, still placeholders with `clinicallyApproved: false`; set each as the next action when the matching track starts. | `Infrastructure/Pathways/breast-screening.json`, `demo/breast-screening.json`, `Features/Events/Handlers/ConsultationUpdateHandler.cs`, `ReferralUpdateHandler.cs` | WS1, WS2 |
| C13 | Caregiver and consent (decision 7) | Add `RecipientKind.Caregiver`; add a `Consent` record on the case (caregiver contact, granted at, withdrawn at, evidence reference) carried by a new `ConsentRecorded` event with a `ConsentPayload` (chosen over a field on `ScreeningResultPayload` because consent is granted and withdrawn on its own timeline, each with its own evidence and audit entry); WS3 maps a FHIR `Consent` resource to it; `RecipientResolver` sends appointment date, time and place only to a caregiver with active consent; log each caregiver message as a `NotificationAttempt`; withdrawal stops further sends. Delivery stays simulated: add an `Email` channel to the patient-channel simulator's inbox and leave `EmailNotificationProvider` a stub, as `AGENTS.md` keeps real email out of scope. | `Domain/Recipients/`, `Domain/Cases/Case.cs`, `Contracts/Events/`, `Features/Events/Handlers/ConsentHandler.cs`, `Features/Fhir/Inbound/ConsentMapper.cs` (new), `Features/Notifications/RecipientResolver.cs`, `Infrastructure/Templates/en/` (a caregiver template), patient-channel `State/InboxChannel.cs`, `Inbound/` | WS1, WS2, WS3, WS4, WS8 |

C10, C11 and C13 each cross several workstreams. Agree the Domain and contract changes (WS1, WS2) first; the simulators and read endpoints follow.

## Section 4: What the product manager should update

These are changes to [ui-design/README.md](ui-design/README.md) and the screen images. The code is the reference for the names; the PM owns the words.

| # | Section | Update |
|---|---|---|
| P1 | Flow (stages table) | Keep the ten as display labels and add a column mapping each to the five `CaseStage` values and the track status that produces it (decision 1). Replace the "Entry event" column with the nine `CaseEventType` values and their statuses. |
| P2 | Screens 5 to 10, event names | Use the real type and status: `ConsultationUpdated/Requested`, `ConsultationUpdated/OutcomeRecorded`, `ReferralUpdated/Sent`, `ReferralUpdated/Rejected`, `AppointmentUpdated/Scheduled`, `OutcomeRecorded`, `ClosureDecided`, `NotificationUpdated/Delivered`, `/Failed`, `/Acknowledged`, `/Understood`, `/CallRequested`, `HandoffSummaryApproved`. Remove the asterisks on names that now exist. Rename `ConfidenceRated` to `NotificationUpdated/ConfidenceRated` (C10). Remove `AppointmentConfirmed` (decision 2). |
| P3 | Who sees what | Add a "Role in CareLoop" column: nurse navigator = `nurse-navigator`, screening doctor = `clinician`, receiving doctor = `receiving-clinician`, lab = `lab`, facility lead = `facility-lead`, programme coordinator = `coordinator` (decision 5, C11). Patient and family member are recipients, not roles. |
| P4 | Who sees what, "Everything is recorded" and "Outside your team needs a reason" | Note that view logging and team scoping are C4, not yet built, and that "give a reason" to open a case outside the team has no endpoint. Either descope the reason prompt or add it to C4. |
| P5 | Screen 2 | "Reads `GET /cases/{id}` and `GET /cases/{id}/timeline`" is right. Add that the source system on each entry depends on C3. |
| P6 | Screen 3 | List the exact measures with their denominators so WS6 can build C5 to them. Name which come from `GET /metrics` and which from `POST /metrics/simulation`. |
| P7 | Screen 4 | Change `GET /programmes`\* to `GET /programmes` and `GET /programmes/{pathwayId}` (C1). State that citations and change history depend on C2. |
| P8 | Screen 5 | A patient's question arrives as `NotificationUpdated/CallRequested`, which raises a `CallRequested` task for the case owner with a due time from the `call-patient` deadline. There is no "answered" event for a call-back; the nurse records `Acknowledged` or `Understood` after the call. Reword. Colleague questions use the `answer-consultation` deadline and referrals the `respond-to-referral` deadline, both placeholders (decision 6, C12). |
| P9 | Screen 6 | Move "Approve summary" to the CareLoop case screen (screen 2) and show the approved text as read-only on screen 6 (decision 4). The HIS receives the approved summary as a `DocumentReference`. |
| P10 | Screen 7 | `ReferralAccepted` and `AppointmentScheduled` exist as `ReferralUpdated/Accepted` and `AppointmentUpdated/Scheduled`. Labs using the same screen for test requests has no distinct event; a test request is a consultation or referral. Say which. |
| P11 | Screen 8 | Matches. Rename `ClosureAuthorised` to `ClosureDecided` and `EvidenceAttached` to `OutcomeRecorded`. |
| P12 | Screen 9 | Note that two-way SMS depends on C7 in the simulator and on a provider agreement in production. Automated voice calls have no channel in `NotificationChannel`; mark as future. |
| P13 | Screen 10 | Reword "I will come" as acknowledgement; it sends `NotificationUpdated/Acknowledged` only (decision 2). The confidence rating sends `NotificationUpdated/ConfidenceRated` with a 1 to 5 value (C10). |
| P14 | Screen 11 | Change "Arabic works from the start" to deferred, matching the "Not in scope yet" list in `AGENTS.md`. Keep the screen as the target design. |
| P15 | Screen 12 | Keep in scope (decision 7). State that consent is recorded as a `Consent` on the case, that the caregiver receives date, time and place only, and that in the prototype the email is shown in the patient-channel simulator, not sent (C13). Specify how consent is captured and withdrawn, since the screen shows only the result. |
| P16 | Screen 13, "No internet" | Note that offline caching belongs to the health app, not to CareLoop or the simulators. |
| P17 | Screens table, "Built in" column | Correct: screens 5 to 8 are built in `src/simulators/his/HisSimulator/ClientApp`, screens 9 to 12 in `src/simulators/patient-channel/PatientChannelSimulator/ClientApp`. |
| P18 | Open questions | Remove the stage, caregiver permission and two-way text questions, now decided. Add: who approves the consent wording, and which role may withdraw consent on a patient's behalf. |

## Section 5: Initial Decisions taken

Taken by the product owner on 13 November 2026 after the engineering recommendation. Where the decision differs from the recommendation, the cost is noted.

| # | Decision | Outcome | Consequence |
|---|---|---|---|
| 1 | Stage model | Five stages in code; the ten in the design are display labels derived from stage plus track statuses. | Label map in the web app (C9). PM updates the flow table (P1). `architecture.md` and `contracts/README.md` keep their ten-stage text as the display vocabulary. |
| 2 | Patient confirms an appointment | Dropped. "I will come" is an acknowledgement. | No code change. PM rewords screen 10 (P13). |
| 3 | Confidence rating | Build it. | C10, across WS1, WS2, WS3, WS4, WS6, WS8 and WS10. Recommendation was to descope; the owner confirmed it is a measure the demo must report. |
| 4 | Where the AI summary is approved | CareLoop web app. | The case screen gets draft and approve controls (C9). HIS simulator stays FHIR-only. PM updates screen 6 (P9). |
| 5 | Roles | Add `nurse-navigator`, `receiving-clinician` and `lab`. | C11, across WS1, WS6 and WS7, plus the shared `DemoRoles` file. Recommendation was three roles; the owner wants the personas enforced by the API, not only shown. |
| 6 | Consultation reply deadline | Per-kind deadlines. | C12 in WS1 and WS2. Both values stay placeholders until a clinical adviser approves them. |
| 7 | Caregiver and consent | Build it. | C13, across WS1, WS2, WS3, WS4 and WS8. Recommendation was to defer; the owner wants screen 12 in the prototype. Real email stays out of scope: the caregiver message is shown in the patient-channel simulator. |

## Section 6: Other documents to align

| Document | Change |
|---|---|
| [architecture.md](architecture.md) | The ten-stage table is now the display vocabulary over the five Domain stages (decision 1). A note says so. |
| [contracts/README.md](../contracts/README.md) | The stage-entry event names are display labels; the nine `CaseEventType` values are the contract (decision 1). A note says so. |
| [workstreams.md](workstreams.md) | Items C1 to C13 are listed under their workstreams, and decisions 1 to 7 are in the contract points table as settled. |
| [component-register.md](component-register.md) | Its ten-stage and caregiver wording now matches decisions 1 and 7. A note points here. |
| `src/web/README.md` | Says the dashboard is read-only. Add that the case screen drafts and approves the AI summary (decision 4). |
| [AGENTS.md](../AGENTS.md) | "Not in scope yet" keeps real email out. Add a line that caregiver messages are simulated in the prototype (decision 7). Shared file; agree first. |
