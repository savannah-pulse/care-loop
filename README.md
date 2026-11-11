# CareLoop

CareLoop is a synthetic prototype for coordinating one breast-screening follow-up pathway across existing health systems. It makes ownership, handoffs, deadlines, exceptions, and evidence visible; provider HIS/EMRs and approved patient channels remain the systems of record.

This repository is the shared starting point for the prototype. The detailed [architecture](docs/architecture.md), [component workstreams](docs/workstreams.md), and [development guide](docs/development.md) define how members can work independently while integrating against common contracts.

## Prototype boundaries

- Use synthetic data and simulator events only. Do not add real patient information, credentials, or live-system endpoints.
- Store coordination metadata and references, not clinical records. Do not duplicate clinical data entry.
- CareLoop does not diagnose, interpret raw results, set urgency, make clinical decisions, or create clinical outcomes.
- Delivery, patient acknowledgement, stated understanding, consent, referral acceptance, appointment booking, attendance, and care delivered are distinct facts.
- Every open obligation has an accountable owner or is visible in an owned queue. Rejection, delay, missing evidence, and failed contact never silently complete work.
- Only an authorized human can close a case, with pathway evidence or a documented exception.
- AI output is a reviewable draft. It cannot change source facts, set urgency, close cases, or send messages.

## Planned repository map

The source folders will be created as workstreams begin; this bootstrap does not claim that executable services are already present.

```text
contracts/                 Shared event and API contract guidance
docs/                      Architecture, workstreams, and development practices
src/
  api/                     ASP.NET Core orchestration and coordination API
  web/                     React + TypeScript demo UI
  simulators/
    his/                   Synthetic HIS event producer
    patient-channel/       Synthetic delivery and response producer
  worker/                  Deterministic deadline and missing-event checks
tests/
  contracts/               Contract and schema compatibility checks
  integration/             End-to-end synthetic journey checks
infra/local/               Local-only orchestration when implementation starts
```

The directories are workstream boundaries, not independent sources of truth. The API owns validation, correlation, idempotency, authorization, transitions, and audit. Dashboards and patient views read the API's case projection; simulators emit events and never write cases directly.

## Getting started

This is the bootstrap phase; executable services and a local run command have not yet been added. Start by:

1. Reviewing [the architecture and open decisions](docs/architecture.md).
2. Taking or agreeing a workstream in [the component register](docs/workstreams.md).
3. Agreeing the event envelope and ownership/closure rules before parallel implementations diverge.
4. Following the [development guide](docs/development.md) when adding code or contracts.

The intended MVP stack is React and TypeScript for the demo UI, ASP.NET Core for the orchestration API, a relational coordination store, and a scheduled worker. The team should select the database based on familiarity before implementation; no database, cloud host, or live integration is configured by this bootstrap.

## Demo acceptance gate

The prototype should demonstrate one synthetic case traversing all ten stages from `RESULT_RECEIVED` to `CLOSED`, and a separate stalled or rejected handoff that remains open, owned, and escalated. Replaying a source event must not create a duplicate case, and closure must fail without authorized evidence.
