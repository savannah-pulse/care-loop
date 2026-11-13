# Patient-channel simulator

> **Simulated data only.** Every patient, message and case handled here is synthetic. Sample
> identifiers are prefixed `SYN-`, names start with "Synthetic", and every record carries a
> `simulated` marker. Never point this service at a live endpoint.

A stand-in for the patient-facing channel (app, SMS, portal) used to demonstrate CareLoop
(workstream **WS8**; paths below are relative to the repository root). CareLoop reaches it through its simulator notification provider, and the
simulator calls back on that provider's callback address, the way an SMS or email gateway would.
It shares **no code** with `src/api/` or the HIS simulator: it builds and parses FHIR itself with the
Firely SDK.

**Status: scaffold.** Structure, contracts, documentation and DI wiring exist. Behaviour does not:
every service method throws `NotImplementedException("WS8: ...")` and every route except
`GET /health` returns `501 Not Implemented`. Find the work with `grep -r "TODO(WS-8)" src/simulators/patient-channel tests/simulators/patient-channel`.

Screen designs for this simulator are screens 9 to 12 in [docs/ui-design](../../../docs/ui-design/README.md).

## What it does (when built)

| Folder | Direction | Purpose |
|---|---|---|
| `Inbound/` | CareLoop → patient channel | Receives `CommunicationRequest` (patient notification requests) into a simulated **patient inbox**; routes the UI uses to list and act on messages. |
| `Outbound/` | patient channel → CareLoop | Posts `Communication` to CareLoop's simulator-provider callback (not to `/fhir`): delivery (`completed`), failure (`not-done` + reason), and patient responses whose category distinguishes **acknowledgement**, **teach-back** and **call-me**. Reads case status from `GET {FhirBaseUrl}/Task/{id}`. |
| `Scenarios/` | | Loads the shared scenario files and, when a run starts, puts each scenario patient's `behaviour` in force. It performs no steps and sends nothing by itself. |
| `State/` | | In-memory inbox, FHIR message log, scenario runs, per-patient behaviours and the record of every reaction. |
| `ClientApp/` | | React UI: patient inbox, case status, patient behaviours, scenario runner, FHIR message log. |

Safety meaning carried by the design (see the XML documentation for detail):

- **Delivery is not acknowledgement.** A delivered message stays unacknowledged until the patient
  acts; a delivery report never carries a patient-response category.
- **The simulator never rewrites content.** It shows exactly what CareLoop rendered, so a reviewer
  can check that an SMS-like channel received only the SMS-safe text.
- **The simulator plays the patient by reacting.** Each patient has a behaviour: what the channel
  reports (delivered, failed, or nothing) and what the patient then does (nothing, acknowledge,
  confirm understanding, or ask for a call), optionally after a delay or only on a reminder. It is
  applied when a notification arrives. If CareLoop sends nothing, the patient does nothing, so a
  test cannot pass on a notification that was never sent.
- **Behaviour is per patient.** A patient with no behaviour of their own gets the default, which
  is deliver and do not respond. A response also comes from a patient action in the UI.
- **Time comes from `ISimulatorClock`**, never from the system clock directly.

## Run

Prerequisites: .NET SDK 10, Node 22 (>= 22.22.2) with corepack (do not run `corepack enable`;
invoke Yarn as `corepack yarn`).

```bash
# .NET host on http://localhost:5082
dotnet run --project src/simulators/patient-channel/PatientChannelSimulator

# ClientApp dev server (proxies /api, /fhir, /scenarios, /health, /openapi to :5082)
cd src/simulators/patient-channel/PatientChannelSimulator/ClientApp
corepack yarn install
corepack yarn dev
```

The .NET host serves the built ClientApp from `wwwroot` with single-page-app fallback when that
folder exists (the Docker image copies `ClientApp/dist` there). Without it the API still runs and
`/` returns 404.

Docker (from the repository root; the build context must be the repository root):

```bash
docker compose up --build patient-channel-simulator   # http://localhost:5082
# or
docker build -f src/simulators/patient-channel/Dockerfile -t careloop-patient-channel .
```

## Test

```bash
# from the repository root (CareLoop.sln also builds the API and the other simulator)
dotnet build src/simulators/patient-channel/PatientChannelSimulator && dotnet build tests/simulators/patient-channel/PatientChannelSimulator.Tests   # 0 warnings; missing XML docs (CS1591) are errors
dotnet test tests/simulators/patient-channel/PatientChannelSimulator.Tests   # planned behaviours are named, skipped tests

cd src/simulators/patient-channel/PatientChannelSimulator/ClientApp
corepack yarn lint          # ESLint: TSDoc required on every export, --max-warnings 0
corepack yarn typecheck
corepack yarn test          # Vitest; planned behaviours are it.todo
corepack yarn build         # outputs dist/
corepack yarn format:check  # Prettier
```

## Configuration

| Key | Environment variable | Default | Meaning |
|---|---|---|---|
| `CareLoop:FhirBaseUrl` | `CareLoop__FhirBaseUrl` | `http://localhost:5080/fhir` | Base URL of CareLoop's FHIR R4 endpoints, used to read case status. |
| `CareLoop:NotificationCallbackUrl` | `CareLoop__NotificationCallbackUrl` | `http://localhost:5080/notifications/inbound/simulator` | Callback of CareLoop's simulator notification provider. Delivery reports and patient responses are posted here. |
| `Scenarios:Directory` | `Scenarios__Directory` | `../../scenarios` (relative to the content root; `/app/scenarios` in Docker) | Folder holding the shared scenario JSON files, `src/simulators/scenarios`. |
| `Scenarios:RealSecondsPerScenarioHour` | `Scenarios__RealSecondsPerScenarioHour` | `2` | Pacing of a scenario run: real seconds per scenario hour. A scenario patient's `respondAfter` is compressed by it when a run starts. It must match the factor CareLoop's demo pathway was shortened by (1800, which is 2 seconds per hour) and the other simulator's setting. |
| | `ASPNETCORE_URLS` | `http://localhost:5082` (launch profile); `http://+:8080` in Docker | Listen address. |

No secrets are needed.

## Routes

All routes except `/health` and `/openapi/v1.json` currently return `501` with
`{ "status": "not-implemented", "route": "...", "workstream": "WS8", "synthetic": true }`.

| Method | Route | Purpose |
|---|---|---|
| GET | `/health` | Liveness: `{ "status": "ok", "service": "patient-channel-simulator", "synthetic": true }`. |
| GET | `/openapi/v1.json` | OpenAPI document. |
| POST | `/fhir/{resourceType}` | FHIR inbound from CareLoop; `CommunicationRequest` lands in the patient inbox. |
| GET | `/api/inbox` | List simulated inbox messages. |
| GET | `/api/inbox/{messageId}` | One inbox message. |
| POST | `/api/inbox/{messageId}/acknowledge` | Patient acknowledges → acknowledgement `Communication`. |
| POST | `/api/inbox/{messageId}/understand` | Patient teach-back ("I understand") → teach-back `Communication`. |
| POST | `/api/inbox/{messageId}/call-me` | Patient asks for a call → call-me `Communication`. |
| GET | `/api/patient-behaviours` | Patients that have a behaviour of their own. |
| GET | `/api/patient-behaviours/default` | The behaviour used for every other patient. |
| PUT | `/api/patient-behaviours/default` | Change the default behaviour. |
| PUT | `/api/patient-behaviours/{patientSyntheticId}` | Set how one patient reacts. |
| DELETE | `/api/patient-behaviours/{patientSyntheticId}` | Remove a patient's behaviour; the default applies again. |
| GET | `/api/fhir-log` | FHIR message log. |
| GET | `/api/case-status/{taskId}` | Patient case status from the CareLoop case `Task`. |
| GET | `/scenarios` | List scenarios. |
| POST | `/scenarios/{name}/run` | Put the scenario's patient behaviours in force. |
| GET | `/scenarios/runs/{runId}` | The run's patients and how they have reacted so far. |

ClientApp routes (`/`, `/case-status`, `/patient-behaviours`, `/runner`, `/message-log`) are
handled by the single-page-app fallback and must not start with `/api`, `/fhir`, `/scenarios` or
`/health`.

## Open points for WS8

- The `Communication.category` codes in `Outbound/CommunicationCodes.cs` are **provisional** and
  must be agreed with CareLoop's `StatusMap` (the simulator shares no code with `src/api/`).
- The FHIR version (R4, Firely `Hl7.Fhir.R4`) is an assumption still to be confirmed.
