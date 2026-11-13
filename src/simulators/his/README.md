# HIS simulator

A stand-in for a hospital information system (HIS/EMR), used to demonstrate CareLoop without any
real hospital system. Workstream **WS7**. Paths below are relative to the repository root.

Screen designs for this simulator are screens 5 to 8 in [docs/ui-design](../../../docs/ui-design/README.md).

> **Synthetic data only.** Every patient, member of staff, identifier and message is simulated.
> Identifiers are prefixed `SYN-`, names start with "Synthetic", and every record and response
> type carries a `synthetic: true` marker. Clinical values (urgency, result category, evidence kind,
> closure reason) are placeholder codes passed through from scenario files; the simulator never
> invents clinical content and never decides urgency or closure.

## Status

**Scaffold.** Structure, contracts, dependency injection and documentation are in place. Behaviour
is not: service methods throw `NotImplementedException("WS7: Class.Method")` and every route except
`GET /health` answers `501 Not Implemented` (as an RFC 9457 problem). Each stub is marked
`// TODO(WS-7): ...`; find them all with `grep -rn "TODO(WS-7)" src/simulators/his tests/simulators/his`.

Works today: `GET /health`, `SystemSimulatorClock`, the DI container, scenario-file deserialization
into the `Scenarios/` records, and static serving of the built ClientApp with SPA fallback.

## What it does (when implemented)

The simulator talks to CareLoop **only through FHIR R4** and shares no code with `src/api/`. It builds
and parses FHIR itself with the Firely SDK, as a real hospital system would.

| Folder | Direction | Purpose |
|---|---|---|
| `Outbound/` | HIS to CareLoop | One builder per resource kind, `CareLoopFhirClient` (typed `HttpClient`), `OutboundEventSender`, and the manual clinician action routes |
| `Inbound/` | CareLoop to HIS | `POST`/`PUT /fhir/...` receiver, staff inbox and FHIR message log routes |
| `Scenarios/` | | Records mirroring `src/simulators/scenarios/scenario.schema.json`, `ScenarioLoader`, `ScenarioRunner`, scenario routes |
| `State/` | | In-memory records and `SimulatorStore` (staff inbox, message log, simulated cases, scenario runs) |
| `Time/` | | `ISimulatorClock` and `SystemSimulatorClock`; nothing else reads the system time |
| `ClientApp/` | | React UI: scenario runner, staff inbox, FHIR message log, "act as clinician" |

Outbound FHIR (HIS to CareLoop, `POST`/`PUT {CareLoop:FhirBaseUrl}/{resourceType}`):

| Event | Resources | Builder |
|---|---|---|
| Screening result | `DiagnosticReport` with `Observation`s | `ScreeningResultBuilder` |
| Clinical review | `Task` (completed, priority = reviewed urgency) | `ClinicalReviewTaskBuilder` |
| Consultation | `ServiceRequest` + `Task` (requested, accepted, rejected, completed) | `ConsultationBuilder` |
| Referral | `ServiceRequest` + `Task` (same statuses) | `ReferralBuilder` |
| Appointment | `Appointment` (booked, fulfilled, noshow) | `AppointmentBuilder` |
| Outcome | `Procedure` or follow-up `DiagnosticReport` | `OutcomeBuilder` |
| Closure | case `Task` completed + `Provenance` naming the authorizer | `ClosureBuilder` |

Inbound FHIR (CareLoop to HIS): escalation, handoff and call-request `Task`s, handoff-summary
`DocumentReference`, and staff notifications ("a case needs your action" plus a link, no patient
detail) are filed into the **staff inbox**. Receiving a message never accepts or acknowledges anything.

## Routes

| Method | Route | Purpose | Now |
|---|---|---|---|
| `GET` | `/health` | Liveness: `{ "status": "ok", "service": "his-simulator", "synthetic": true }` | 200 |
| `POST` | `/fhir/{resourceType}` | Receive a FHIR resource from CareLoop | 501 |
| `PUT` | `/fhir/{resourceType}/{id}` | Receive a new state of a resource from CareLoop | 501 |
| `GET` | `/staff-inbox` (`?recipient=`) | List staff inbox items | 501 |
| `GET` | `/staff-inbox/{id}` | One staff inbox item | 501 |
| `POST` | `/staff-inbox/notifications` | Direct staff notification intake (non-FHIR seam; see assumptions) | 501 |
| `GET` | `/fhir-log` (`?direction=sent\|received&caseRef=`) | FHIR messages sent and received | 501 |
| `GET` | `/cases` | Simulated cases a clinician can act on | 501 |
| `POST` | `/actions/clinical-review` | Act as clinician: record a review | 501 |
| `POST` | `/actions/referral-decision` | Act as clinician: accept or reject a referral | 501 |
| `POST` | `/actions/outcome` | Act as clinician: record an outcome | 501 |
| `POST` | `/actions/closure` | Act as clinician: authorize closure | 501 |
| `GET` | `/scenarios` | List scenarios | 501 |
| `POST` | `/scenarios/{name}/run` | Start a scenario run (HIS steps only) | 501 |
| `GET` | `/scenarios/runs/{runId}` | Run progress | 501 |
| `GET` | anything else | Built ClientApp from `wwwroot` (SPA fallback to `index.html`; 404 if `wwwroot` is absent) | works |

## Run

Prerequisites: .NET SDK 10, Node 22 with corepack (do not run `corepack enable`; call `corepack yarn`).

```sh
# API on http://localhost:5081
dotnet run --project src/simulators/his/HisSimulator

# ClientApp dev server on http://localhost:5181, proxying API calls to :5081
cd src/simulators/his/HisSimulator/ClientApp
corepack yarn install
corepack yarn dev
```

To serve the UI from the API itself, run `corepack yarn build` and copy `ClientApp/dist` to
`src/simulators/his/HisSimulator/wwwroot` (git-ignored). The Docker image does this for you.

With Docker (from the **repository root**; the build context must be the root):

```sh
docker compose up --build his-simulator      # http://localhost:5081
```

## Test

```sh
# from the repository root (CareLoop.sln also builds the API and the other simulator)
dotnet build src/simulators/his/HisSimulator && dotnet build tests/simulators/his/HisSimulator.Tests   # 0 warnings: missing XML docs (CS1591) are errors
dotnet test tests/simulators/his/HisSimulator.Tests

# from src/simulators/his/HisSimulator/ClientApp/
corepack yarn lint                # fails on any undocumented export or invalid TSDoc
corepack yarn typecheck
corepack yarn test
corepack yarn build
corepack yarn format:check
```

Planned behaviours exist as named, skipped tests (`[Fact(Skip = "WS7: not implemented")]`, `it.todo`).
Implement the behaviour, then remove the skip; do not delete or weaken a test to make it pass.

## Configuration

| Key | Environment variable | Default | Meaning |
|---|---|---|---|
| `CareLoop:FhirBaseUrl` | `CareLoop__FhirBaseUrl` | `http://localhost:5080/fhir` | CareLoop's FHIR R4 base URL; the only CareLoop address the simulator uses |
| `Scenarios:Directory` | `Scenarios__Directory` | `../../scenarios` | Folder with the shared scenario JSON files (`src/simulators/scenarios`). Relative paths resolve against the content root (the project folder under `dotnet run`). `/app/scenarios` in Docker |
| `Scenarios:RealSecondsPerScenarioHour` | `Scenarios__RealSecondsPerScenarioHour` | `2` | Pacing of a scenario run: real seconds per scenario hour. The runner waits this long between steps, because CareLoop measures deadlines in real time. It must match the factor CareLoop's demo pathway was shortened by (1800, which is 2 seconds per hour) and the other simulator's setting. |
| | `ASPNETCORE_URLS` | `http://localhost:5081` (launch profile), `http://+:8080` (Docker) | Listen address |

No secrets are needed. There are no real endpoints or credentials anywhere in this project.

## Dependencies

- NuGet: `Hl7.Fhir.R4` 6.8.0 (Firely SDK). Tests: `xunit` 2.9.3, `xunit.runner.visualstudio` 4.0.1, `Microsoft.NET.Test.Sdk` 18.11.1.
- npm (see `ClientApp/package.json` and `yarn.lock`): react, react-dom, react-router-dom, i18next, react-i18next; dev: vite, @vitejs/plugin-react, typescript, eslint, typescript-eslint, eslint-plugin-tsdoc, eslint-plugin-jsdoc, eslint-plugin-react-hooks, eslint-config-prettier, prettier, vitest, jsdom, @types/react, @types/react-dom.

Ask before adding anything else. Never add a project reference to `src/api/`.

## Assumptions to confirm

- FHIR R4 is assumed; the version required by the Abu Dhabi exchange standard is unconfirmed.
- `Observation`s are assumed to travel contained in the screening `DiagnosticReport`.
- Staff notifications may arrive as FHIR (`CommunicationRequest` on `/fhir/...`) or on the plain
  `POST /staff-inbox/notifications` seam; agree which with the owner of CareLoop's `SimulatorNotificationProvider`.
- Whether CareLoop expects `PUT /fhir/{resourceType}` or `PUT /fhir/{resourceType}/{id}` from the simulator.
- How the reviewed urgency placeholder is carried on the review `Task` (priority code or extension).
