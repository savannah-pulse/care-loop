# Component workstreams

Use this register to split implementation into owned components while keeping one case engine and a shared event contract. Story and functional-requirement (FR) references come from the supplied user stories and solution design. The API, web UI, and Python workflow service are scaffolds; a component owner should confirm boundaries and dependencies before implementing the remaining features. Names are intentionally left unassigned. Where this register and [workstreams.md](workstreams.md) differ, the workstreams file is current; the gaps found between the screen designs and the code are in [ui-design-gap-analysis.md](ui-design-gap-analysis.md).

| Workstream | Scope and first deliverable | Stories / requirements | Depends on |
|---|---|---|---|
| Orchestration API and case engine | Typed event intake; validation, correlation, idempotency, guarded transitions, ownership, audit, and case/timeline/exception read endpoints. First: agreed event envelope and replay-safe `RESULT_RECEIVED` → `OWNER_ASSIGNED` path. | US01, US08–US15; FR01–FR08, FR10 | Shared event contract; pathway and role assumptions |
| Coordination store | Event log, coordination projection, source/reference keys, assignments, exceptions, evidence references, and audit. Store no clinical record. First: persistence model supporting replay and timeline reconstruction. | US01, US08–US16; FR02, FR03, FR08–FR10 | API event and projection model; database choice |
| HIS simulator and mock adapter | Synthetic result, review, consultation, referral, appointment, and outcome events; preserve source identifiers and allow deterministic replay. First: one actionable result event and one duplicate replay. | US01, US06–US12; FR01, FR02, FR05, FR06 | Shared envelope; synthetic scenario |
| SMS and email simulator | Simulated text messages and emails to patients, caregivers and staff: delivery, failure, and the patient's reply as typed (CareLoop reads 1 as "I got this message" and anything else as a call request). There is no patient app or portal. Understanding and confidence are recorded by a nurse after a call, in CareLoop. Keep receipt, acknowledgement, understanding, consent, and caregiver messages separate. First: one success path and one failed/unacknowledged path. | US02–US05; FR04 | API communication events; reviewed English copy |
| Deadline and escalation worker | Deterministic checks for overdue, rejected, missing, unassigned, and unacknowledged work; append owned escalation events without advancing the case stage. First: a configurable overdue case test. | US03, US05, US08, US10, US13, US14; FR07 | API/store query; approved clocks and escalation recipients |
| Demo UI | React/TypeScript coordination dashboard, read-only timeline, nurse-recorded call outcomes, exception queues, and synthetic impact view. First: mock-data prototype; replace stubs with API reads as they become available. | US02–US05, US10, US16; FR09, FR10 | Wireframe/data shape; API read contract |
| Python AI workflow | Python service and stage-specific agent interfaces coordinating the ten-stage journey. Agents propose non-clinical follow-up actions and human-reviewable drafts; the .NET API validates every state change. First: stage map, synthetic-only context/proposal types, service health endpoint, and workflow contract tests. | US01–US16; FR02–FR11 | .NET API event/action contract; approved pathway rules; AI provider decision |
| Contract and journey tests | Validate envelope compatibility, replay behavior, guarded ownership/closure, and end-to-end synthetic scenarios. First: reusable fixtures for one successful journey and one stalled handoff. | US01, US08–US16; FR01–FR10 | Shared contracts; stable simulator fixtures |

## Integration milestones

1. **Agree the contract:** envelope, event types, source keys, idempotency, payload ownership, and error behavior. Do this before independent producers emit incompatible events.
2. **Prove intake:** replay one synthetic result, open one case, and show its owner, source provenance, and timeline.
3. **Build in parallel:** UI may use clearly labeled mock data; simulators and worker integrate only through the API contract.
4. **Prove exceptions:** rejected referral, failed/unacknowledged contact, missing event, or overdue action stays open with an accountable next action and escalation.
5. **Prove closure:** reach `CLOSED` only through authorized human action with required evidence or a documented, authorized exception.
6. **Rehearse:** run one full ten-stage synthetic journey and one broken handoff; report synthetic metrics with denominators and assumptions.

## Assignment handoff

Copy this checklist into the work item for each assigned component:

- **Owner / reviewer:**
- **Stories and FRs:**
- **In scope / out of scope:**
- **Inputs and outputs (contract version):**
- **Data and authorization boundary:**
- **Dependencies / decisions needed:**
- **Acceptance evidence and tests:**
- **Demo path / fallback:**

Every work item should identify the user-story outcomes it fulfills, the shared interface it consumes or produces, and what it will not decide or store. Raise changes to a story, clinical assumption, event contract, or ownership rule for team review instead of silently diverging.
