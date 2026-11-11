# Development guide

## Working agreements

- Work in the component folders shown in the root README and keep cross-component interfaces in `contracts/`.
- Agree shared event schemas and state-transition rules before changing their shape. Version intentional breaking changes and update all affected producers, consumers, and fixtures together.
- Keep the UI and simulators independently runnable where practical. The UI can use mock data while backend work proceeds, but label it and replace it through the API contract rather than coupling it to simulator internals.
- Keep configuration explicit. Clinical clocks, urgency, closure evidence, message content, and provider qualifications must come from an approved source; do not fill gaps with invented defaults.
- Keep synthetic cases deterministic and replayable. Use stable source identifiers and test duplicate delivery, out-of-order/missing events, rejection, and closure gates.
- Keep source attribution, actor identity/role, timestamps, and audit reasons on accepted events and coordination decisions.

## Contract-first event checklist

Before adding an event, document:

1. Event type and contract version; source system and stable source event ID.
2. Occurrence and received timestamps, synthetic patient/result references, and actor provenance where relevant.
3. Required event-specific fields and validation rules.
4. Correlation and idempotency behavior, including correction/conflict handling.
5. Which stage or sub-status it may affect, permitted actors, evidence requirements, and whether it creates follow-up work.
6. A valid example and invalid/duplicate examples for contract tests.

Use the contract guidance in `contracts/README.md`. API validation, authorization, idempotency, transition guards, and audit must not exist only in the UI.

## Local safety and configuration

- Never commit real patient data, credentials, connection strings, access tokens, or production endpoints. Use synthetic fixtures and local environment variables; commit only placeholders such as `.env.example` if needed.
- Do not put sensitive clinical details in SMS/email simulator payloads. Treat all generated patient-facing content as a draft until approved.
- Enforce demo roles at the API boundary, not just by hiding UI controls.
- Mark synthetic data and simulated messages in the UI and exported metrics. Keep real-world impact claims out of demo results.
- Database and hosting choices are not set by this bootstrap. Record the team's decision and a reproducible local setup before adding environment-specific files.

## Pull request checklist

- [ ] Linked user stories and FRs are identified; scope stays within the approved synthetic prototype.
- [ ] Shared contracts and affected workstreams are updated together.
- [ ] Authorization, source provenance, idempotency, ownership, audit, and error behavior are covered where applicable.
- [ ] Tests cover the introduced behavior and relevant failure/duplicate path.
- [ ] No real patient data, secret, live endpoint, or unapproved clinical rule was introduced.
- [ ] Demo behavior distinguishes delivery, acknowledgement, understanding, consent, referral acceptance, attendance, care, and closure.
- [ ] Documentation and synthetic demo labels are current.
