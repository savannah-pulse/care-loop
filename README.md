# CareLoop

CareLoop is a synthetic prototype for coordinating one breast-screening follow-up pathway across existing health systems. It makes ownership, handoffs, deadlines, exceptions, and evidence visible; provider HIS/EMRs and approved patient channels remain the systems of record.

This repository contains a runnable API and web scaffold, local container setup, CI and release workflows, and shared workstream documentation. The [architecture](docs/architecture.md), [component register](docs/workstreams.md), and [development guide](docs/development.md) define how members can work independently while integrating against common contracts.

## Prototype boundaries

- Use synthetic data and simulator events only. Do not add real patient information, credentials, or live-system endpoints.
- Store coordination metadata and references, not clinical records. Do not duplicate clinical data entry.
- CareLoop does not diagnose, interpret raw results, set urgency, make clinical decisions, or create clinical outcomes.
- Delivery, patient acknowledgement, stated understanding, consent, referral acceptance, appointment booking, attendance, and care delivered are distinct facts.
- Every open obligation has an accountable owner or is visible in an owned queue. Rejection, delay, missing evidence, and failed contact never silently complete work.
- Only an authorized human can close a case, with pathway evidence or a documented exception.
- AI output is a reviewable draft. It cannot change source facts, set urgency, close cases, or send messages.

## Repository map

```text
CareLoop.sln                .NET solution
contracts/                  Shared event and API contract guidance
docs/                       Architecture, workstreams, and development practices
infra/local/                Local environment notes
src/
  api/CareLoop.Api/         ASP.NET Core API starter and Dockerfile
  web/                      React + TypeScript + Vite starter and Dockerfile
  simulators/
    his/                    Synthetic HIS simulator workstream
    patient-channel/        Synthetic patient-channel workstream
  worker/                   Deadline and escalation workstream
tests/
  api/                      API smoke tests
  contracts/                Contract and schema compatibility workstream
  integration/              Synthetic journey test workstream
.github/workflows/          Pull-request CI and tagged release packaging
```

The directories are workstream boundaries, not independent sources of truth. The API currently exposes only a health endpoint; case workflows and simulators remain unimplemented. Once added, the API owns validation, correlation, idempotency, authorization, transitions, and audit.

## Getting started

The prerequisites are .NET 10 SDK, Node.js 24, npm, and (for containers) Docker Compose.

Run the API and its smoke tests:

```sh
dotnet test CareLoop.sln
dotnet run --project src/api/CareLoop.Api
```

In another terminal, run the web starter:

```sh
cd src/web
npm ci
npm run dev
```

Or build and start both starter services with `docker compose up --build`; open the web starter at `http://localhost:4173` and the API health check at `http://localhost:8080/health`.

The selected stack is React/TypeScript and ASP.NET Core. The coordination database, cloud host, identity model, and production deployment target remain team decisions; no live integration or clinical workflow is implemented by this scaffold.

## Demo acceptance gate

When the prototype is implemented, it should demonstrate one synthetic case traversing all ten stages from `RESULT_RECEIVED` to `CLOSED`, and a separate stalled or rejected handoff that remains open, owned, and escalated. Replaying a source event must not create a duplicate case, and closure must fail without authorized evidence.

## License

This project is licensed under the [MIT License](LICENSE).
