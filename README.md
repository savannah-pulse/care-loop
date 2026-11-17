# CareLoop

CareLoop is a coordination and accountability layer for screening follow-up. Every actionable screening result becomes
a **case** that always has a stage, an owner, a deadline and, at the end, closure evidence. The prototype targets one
Abu Dhabi breast-screening follow-up pathway.

CareLoop is **not** a clinical system. Hospital systems remain the systems of record. CareLoop correlates events,
tracks ownership and deadlines, raises escalations and blocks unsafe closure.

> **Current stage: prototype on synthetic data.** CareLoop is being built to run in production on real patient data.
> The API is built as for production; today it is connected to two simulators, which label their own records and
> messages as simulated. Real patient data, credentials and live endpoint addresses must never be committed to this
> repository. See "Before real patient data" in [`AGENTS.md`](AGENTS.md) for what a production deployment needs first.

The [architecture](docs/ai-design/architecture.md), [workstreams](docs/workstreams.md), [AI design set](docs/ai-design/README.md),
[development guide](docs/development.md), and [UI design](docs/ui-design/README.md) define how members work
independently against common contracts. The rules every contributor follows are in [`AGENTS.md`](AGENTS.md).

## Prototype boundaries

- Use synthetic data and simulator events only. Do not add real patient information, credentials, or live-system endpoints.
- Store coordination metadata and references, not clinical records. Do not duplicate clinical data entry.
- CareLoop does not diagnose, interpret raw results, set urgency, make clinical decisions, or create clinical outcomes.
- Delivery, patient acknowledgement, stated understanding, consent, referral acceptance, appointment booking, attendance, and care delivered are distinct facts.
- Every open obligation has an accountable owner or is visible in an owned queue. Rejection, delay, missing evidence, and failed contact never silently complete work.
- Only an authorized human can close a case, with pathway evidence or a documented exception.
- AI output is a reviewable draft. It cannot change source facts, set urgency, close cases, or send messages.

## Implementation status

WS1 to WS6 implement Domain rules, persistence/intake, FHIR, outbox/notifications, deadlines and the staff read
APIs/dashboard. Simulator, AI-summary and end-to-end workstreams still have assigned stubs. Remaining stubs are tagged
`TODO(WS-n)` with named skipped tests. Search for `TODO(WS-` and read [`docs/workstreams.md`](docs/workstreams.md)
for ownership and validation. The dashboard reports unavailable capabilities explicitly.

## Repository map

```text
CareLoop.sln                .NET solution: API, Domain, both simulators and their tests
Directory.Build.props       Shared .NET build rules: XML docs enforced, warnings are errors
AGENTS.md                   The rules for this repository
contracts/                  Shared contract guidance and the API's OpenAPI document (openapi/careloop.json)
docs/                       Architecture, workstreams, development practices, releasing, and UI design
infra/local/                Local environment notes
src/
  api/                      The CareLoop engine (.NET 10): CareLoop.Domain, CareLoop.Api and its Dockerfile
  ai_workflow/              Python AI workflow service, stage/agent contracts, and Dockerfile
  web/                      React + TypeScript + Vite staff dashboard and Dockerfile
  simulators/
    scenarios/              Scripted synthetic journeys shared by both simulators (scenario.schema.json)
    his/                    Stand-in for a hospital information system: HisSimulator (.NET) with a React ClientApp
    messaging/              Messaging simulator (text messages and email): MessagingSimulator (.NET) with a React ClientApp
  worker/                   Deadline and escalation notes; the worker runs inside CareLoop.Api/Worker
tests/
  api/CareLoop.Tests/       xUnit tests, fakes and synthetic FHIR samples for the API and Domain
  simulators/               HisSimulator.Tests and MessagingSimulator.Tests
  contracts/                Contract and schema compatibility workstream
  integration/              Synthetic journey test workstream
.github/workflows/          Pull-request CI and tagged release packaging
```

| Part | README |
|---|---|
| API and Domain | [`src/api/README.md`](src/api/README.md) |
| Dashboard | [`src/web/README.md`](src/web/README.md) |
| AI workflow | [`src/ai_workflow/README.md`](src/ai_workflow/README.md) |
| HIS simulator | [`src/simulators/his/README.md`](src/simulators/his/README.md) |
| Messaging simulator | [`src/simulators/messaging/README.md`](src/simulators/messaging/README.md) |

```text
HIS simulator ──FHIR R4──▶ ┌──────────────┐ ◀──REST── dashboard (src/web/)
                           │ CareLoop API │ ◀──proposals── AI workflow (src/ai_workflow/)
SMS and email  ◀─JSON──▶  └──────┬───────┘
simulator                         │
                              PostgreSQL
```

The HIS simulator talks to CareLoop only through `/fhir/...`. CareLoop reaches patients, caregivers and staff by text
message and email only (there is no patient app or portal): its `Sms` and `Email` providers post plain JSON to the SMS
and email simulator, which calls back on `/notifications/inbound/sms` and `/email`. Neither simulator shares code
with CareLoop. The .NET API owns validation, correlation, idempotency, authorization, transitions, and audit; the
Python workflow proposes actions for the API and authorized people to review.

## Start everything

Prerequisite: Docker with Compose.

```sh
cp .env.example .env
```

```sh
docker compose up --build
```

| Service | URL | Health |
|---|---|---|
| API | http://localhost:8080 | `/health` |
| Dashboard | http://localhost:4173 | |
| AI workflow | http://localhost:8081 | `/health` |
| HIS simulator | http://localhost:5081 | `/health` |
| Messaging simulator | http://localhost:5082 | `/health` |
| PostgreSQL | localhost:5432 | |

Ports are set in `.env`. The compose stack points the API at the demo pathway folder, whose deadlines are shortened for
live runs, and applies database migrations at startup.

## Work on one part

Prerequisites: .NET 10 SDK, Node.js 24 with npm for the dashboard and Corepack/Yarn for the simulator
ClientApps (call `corepack yarn`, do not run `corepack enable`), Python 3.13 for the AI workflow.

| Part | Build and test |
|---|---|
| API, Domain and both simulators | `dotnet build CareLoop.sln && dotnet test CareLoop.sln` |
| API only | `dotnet run --project src/api/CareLoop.Api` (http://localhost:5080) |
| HIS simulator | `dotnet run --project src/simulators/his/HisSimulator` (http://localhost:5081) |
| Messaging simulator | `dotnet run --project src/simulators/messaging/MessagingSimulator` (http://localhost:5082) |
| Simulator ClientApps | in each `ClientApp/`: `corepack yarn install && corepack yarn lint && corepack yarn typecheck && corepack yarn test && corepack yarn build` |
| Dashboard | `cd src/web && npm ci && npm run dev` |
| AI workflow | see [`src/ai_workflow/README.md`](src/ai_workflow/README.md) |

The HIS simulator supports headless HIS replay, source clinician actions, a staff inbox for what CareLoop sends it as FHIR, and exact FHIR logs. Staff notices go by email and show in the messaging simulator.
See its [demo and test guide](src/simulators/his/README.md). Replay completion does not assert completed care:
patient behaviour, cohort expansion and full screening-to-care E2E assertions remain WS8/WS10.

When the API's routes or contracts change, rebuild the API: the build rewrites `contracts/openapi/careloop.json`, and
CI fails if the committed document is out of date.

## Standards that fail the build

- .NET: nullable enabled, warnings are errors, and a missing XML documentation comment (`CS1591`) is an error. Test
  projects are exempt from `CS1591` only.
- TypeScript (simulator ClientApps): strict mode, and ESLint requires a TSDoc comment on every export.
- CI builds and tests the whole solution, both ClientApps, the dashboard and the Python workflow on every pull request.

## Demo acceptance gate

When the prototype is implemented, it should demonstrate one synthetic case traversing all ten stages from `RESULT_RECEIVED` to `CLOSED`, and a separate stalled or rejected handoff that remains open, owned, and escalated. Replaying a source event must not create a duplicate case, and closure must fail without authorized evidence.

## Open questions

These are unconfirmed and must not be treated as decided: the FHIR version required by the Abu Dhabi Department of
Health exchange standard (R4 assumed), the database (PostgreSQL assumed), the stage entry and exit rules, and every
clinical deadline. All pathway values are placeholders until a clinical adviser approves them.

## License

This project is licensed under the [MIT License](LICENSE).
