# FHIR sample resources (synthetic)

One minimal, valid FHIR R4 resource per resource type CareLoop exchanges across the FHIR boundary. Messages to
patients, caregivers and staff are not FHIR: they go by text message and email as plain JSON (see `src/api/README.md`).
Every file is **synthetic**: identifiers are prefixed `SYN-`, every resource carries the
`simulated` tag, and codes are placeholders rather than clinical codes.

`FhirSampleFilesTests` checks that every file here parses as FHIR R4 and is tagged as simulated. Each inbound
mapper's tests start from its sample here and vary it in code.

## Wire profile (what a sending system must put where)

This is the contract the HIS simulator (WS7) builds against. Every code and URL below is a constant in
`src/api/CareLoop.Api/Features/Fhir/StatusMap.cs`; `B` stands for `https://careloop.invalid/fhir`, a reserved
domain to replace before live exchange.

**Every inbound resource**

- `POST /fhir/{type}` or `PUT /fhir/{type}/{id}` with `Content-Type: application/fhir+json`.
- `meta.source` is the sending system's identifier, equal to its key under `ExternalSystems:Systems`
  (`his-simulator` in development). Without it the request is refused with 400.
- `meta.versionId` changes whenever the state changes. The source event ID is `{type}/{id}/_history/{versionId}`;
  without a version CareLoop falls back to `meta.lastUpdated`, then to a hash of the content, so an identical
  resend is always a duplicate (200) and never a second event.
- A person who acted is a `Reference` (`Practitioner/{id}`) carrying two extensions: `B/StructureDefinition/actor-role`
  (`valueCode`, a CareLoop role such as `clinician`) and `B/StructureDefinition/actor-facility` (`valueString`).
  A reference without a role is read as "no actor", and the engine then refuses any step that needs one.
- Every resource names the screening result it belongs to, `DiagnosticReport/{resultId}`, in the field listed below.

| Resource | Means | Result link | What CareLoop reads |
|---|---|---|---|
| `DiagnosticReport` (final, amended, corrected, appended) | New screening result: opens a case | none (it is the result) | `subject` (contained `#id` or `Patient/{id}`); `category` in `B/CodeSystem/pathway` (pathway id); `performer` `Organization/{facility}`; `resultsInterpreter` with role (named owner). Never `conclusion` or `Observation` values. |
| `DiagnosticReport` with the `B/StructureDefinition/screening-result` extension | Follow-up report: outcome evidence | the extension (`valueReference`) | `code` in `B/CodeSystem/evidence-kind` (C21) |
| `Patient` (contained in the report, or read from the source) | Demographics (C20) | n/a | `name` (official first), `identifier` in `B/sid/mrn`, preferred `communication.language`, `telecom` (`sms` and `email` only; any other system is not a way CareLoop can reach the patient) |
| `Task` with `code` `B/CodeSystem/task-code#clinical-review`, `completed` | Clinical review | `focus` or `basedOn` | `owner` (reviewer, with role); urgency from the `B/StructureDefinition/reviewed-urgency` extension (`valueCode`), else `priority` |
| `ServiceRequest`, `active`, `category` `B/CodeSystem/service-request-category#consultation` or `#referral` | Consultation requested or referral sent | `reasonReference` | `requester`; for a referral, `performer` `Organization/{facility}` |
| `Task` with `code` `#consultation` | Consultation update | `basedOn` | `focus` = the `ServiceRequest`; `requested` + `owner` with role and facility = handoff proposal to that owner; `requested` without owner, `accepted`, `rejected` (`statusReason.text` = reason), `completed` = outcome recorded |
| `Task` with `code` `#referral` | Referral update | `basedOn` | `requested`, `accepted`, `rejected` (reason in `statusReason.text`, receiving facility in `owner` `Organization/{id}`). `completed` is ignored: a referral is never completed care. |
| `Appointment` | booked, fulfilled, noshow | `supportingInformation` | `start` |
| `Procedure`, `completed` | Outcome evidence | `reasonReference` | `code` in `B/CodeSystem/evidence-kind`; `performer.actor` with role |
| `Provenance` containing the case `Task` (`target` = `#id`) | Closure decision | the contained task's `basedOn` | `agent.who` with role = the authorizer; the contained task's `statusReason` in `B/CodeSystem/status-reason` (closure reason) and `output`s typed in `B/CodeSystem/closure-output`: `evidence`, `next-surveillance-date`, `documented-attempt`, `exception-note` |
| `Task` with `code` `#case`, `completed`, `relevantHistory` = contained `Provenance` | Closure decision (same event as above) | `basedOn` | as above |
| `Consent` | Caregiver consent (C13): `active` granted, `inactive` or `rejected` withdrawn | the `screening-result` extension | `provision.actor.reference` = contained `RelatedPerson`, whose first usable `telecom` is the caregiver contact point (`sms` or `email`, as for the patient) |

**Responses.** 201 (applied, with `Location`), 200 (duplicate, or nothing CareLoop tracks), 400 (not FHIR, wrong
type or id, no `meta.source`, a field above missing, no case for the result), 422 (a rule refused the event; it is still
audited), 503 (a referenced `Patient` could not be read). Errors carry an `OperationOutcome`.

**Outbound.** CareLoop `PUT`s to `{baseUrl}/fhir/{type}/{id}` of the system the case came from: the case `Task`
(id = case id, `code` `#case`, stage in `businessStatus` with `B/CodeSystem/case-stage`, `basedOn` the result), notice
`Task`s (`#escalation`, `#call-request`, id `notice-{eventId}`, `focus` the case task) and the approved handoff
summary `DocumentReference` (id `handoff-{summaryId}`). A queue owner is referenced by `identifier` in `B/sid/queue`.
