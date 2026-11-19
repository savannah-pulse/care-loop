# Shared contracts

This directory is the home for versioned interfaces shared by the simulators, orchestration API, worker, and UI. The bootstrap does not declare a final event schema; the team should agree and add it here before parallel event producers and consumers are implemented.

## Proposed event envelope

Every event should carry, at minimum:

- `eventType` and `schemaVersion`
- `sourceSystem` and stable `sourceEventId`
- `occurredAt` (source time) and a separately recorded received time
- Synthetic patient/result/case references needed for correlation
- Event-specific payload validated against its type
- Actor and role provenance where the event represents a human action

Use `(sourceSystem, sourceEventId)` as the initial idempotency key unless the team documents a justified alternative. Preserve original source IDs; never use patient details as a deduplication key. Define correction and conflicting-identity behavior explicitly and route unresolved conflicts to human review.

## Event families in the solution design

> **Current contract.** The API implements nine source event types (`CareLoop.Domain.Events.CaseEventType`) and track statuses. The stage-entry events named below are display labels, not events (decision 1 and section 2 of [docs/ui-design-gap-analysis.md](../docs/ui-design-gap-analysis.md)). The OpenAPI document in `openapi/careloop.json` is the contract.

The design names result, review, patient-channel, consultation, referral, appointment, outcome, and closure families. It also names stage-entry events (`ResultReceived`, `CaseCreated`, `OwnerAssigned`, `PatientNotified`, `PatientAcknowledged`, `ConsultationRequested`, `FollowUpStarted`, `OutcomeDocumented`, `ClosureReviewRequested`, `ClosureAuthorised`) and supporting events such as rejection, missed appointment, evidence attachment, and escalation.

The API owns the mapping from accepted source events to case transitions. Not every event advances a stage: escalation and most exception events add a flag or owned task while the case remains in its current stage. Keep referral sent/accepted separate from attended care, and keep a proposed owner separate from an accepted transfer.

There is no separate AI service: the API itself asks agents in a Microsoft Foundry project, one per text, for three staff-facing texts, sending each agent its plain-text prompt file with the data in place (see [`docs/ai-design/integration.md`](../docs/ai-design/integration.md)). An AI provider receives only the minimum facts the API allows, and returns drafts for staff to read. The .NET API remains the only service that can authorize and persist case changes. Do not allow AI output to bypass authorization, audit, or human review.

## Contract review before merge

- Is the event synthetic and attributable to its source or actor?
- Can it be replayed safely, and how are corrections/conflicts surfaced?
- Which transition or sub-status can it affect, and who is allowed to perform it?
- What evidence/reference is required, and does an incomplete event create visible review work?
- Does it retain an accountable owner and next action on rejection, delay, or failure?
- Are examples and contract tests updated for producers and consumers?
