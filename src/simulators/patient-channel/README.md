# Patient-channel simulator

> **Simulated data only.** Every patient, message and case handled here is synthetic. Sample
> identifiers are prefixed `SYN-`, names start with "Synthetic", and every record carries a
> `simulated` marker. Never point this service at a live endpoint.

A stand-in for the patient-facing channel (health app, patient portal, text message, caregiver email) used to demonstrate CareLoop
(workstream **WS8**; paths below are relative to the repository root). CareLoop reaches it through its simulator notification provider, and the
simulator calls back on that provider's callback address, the way an SMS or email gateway would.
It shares **no code** with `src/api/` or the HIS simulator: it builds and parses FHIR itself with the
Firely SDK.

**Status: built.** Every route answers, the ClientApp has all five screens, and no `TODO(WS-8)`,
skipped test or `it.todo` remains. It has been run against a local CareLoop API: a notification
arrived, and the signed delivery report, acknowledgement, teach-back, call request and confidence
rating were each accepted. The hospital-system events were posted by hand, because the HIS
simulator (WS7) is not built yet.

Screen designs for this simulator are screens 9 to 12 in [docs/ui-design](../../../docs/ui-design/README.md).

## What it does

| Folder | Direction | Purpose |
|---|---|---|
| `Inbound/` | CareLoop → patient channel | Receives `CommunicationRequest` (patient notification requests) into a simulated **patient inbox**; routes the UI uses to list and act on messages. |
| `Outbound/` | patient channel → CareLoop | Posts `Communication` to CareLoop's simulator-provider callback (not to `/fhir`): delivery (`completed`), failure (`not-done` + reason), and patient responses whose category distinguishes **acknowledgement**, **teach-back**, **call-me** and **confidence rating**. Every post is signed. Reads case status from `GET {FhirBaseUrl}/Task/{id}`. |
| `Scenarios/` | | Loads the shared scenario files and, when a run starts, puts each scenario patient's `behaviour` in force. It performs no steps and sends nothing by itself. |
| `State/` | | In-memory inbox, FHIR message log, scenario runs, per-patient behaviours, the record of every reaction and the responses waiting for their due time. Lost on restart by design. |
| `ClientApp/` | | React UI: patient inbox (smartphone, basic phone and caregiver email, after design screens 9, 10 and 12), case status, patient behaviours, scenario runner, FHIR message log. |

Safety meaning carried by the design (see the XML documentation for detail):

- **Delivery is not acknowledgement.** A delivered message stays unacknowledged until the patient
  acts; a delivery report never carries a patient-response category.
- **The simulator never rewrites content.** It shows exactly what CareLoop rendered. Note that the
  text-message inbox shows the standard template, not the SMS-safe one: CareLoop's simulator
  provider allows sensitive content and sends no channel, so the SMS-safe text never reaches it.
- **A caregiver's copy is read-only.** It lands in the email inbox, gets a delivery report and can
  never be answered: only the patient's own response counts.
- **A rating is not an acknowledgement.** A 1 to 5 confidence rating has its own category and
  never changes whether the message is acknowledged.
- **Callbacks are signed, or not sent.** Every `Communication` carries
  `X-CareLoop-Signature: sha256=<hex HMAC-SHA256 of the body>`. With no secret configured the
  simulator sends nothing and says which setting is missing.
- **The simulator plays the patient by reacting.** Each patient has a behaviour: what the channel
  reports (delivered, failed, or nothing) and what the patient then does (nothing, acknowledge,
  confirm understanding, ask for a call, or rate their confidence), optionally after a delay or
  only on a reminder. It is
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
dotnet test tests/simulators/patient-channel/PatientChannelSimulator.Tests   # hosts the real routes in memory with CareLoop stubbed

cd src/simulators/patient-channel/PatientChannelSimulator/ClientApp
corepack yarn lint          # ESLint: TSDoc required on every export, --max-warnings 0
corepack yarn typecheck
corepack yarn test          # Vitest; each screen against a faked host
corepack yarn build         # outputs dist/
corepack yarn format:check  # Prettier
```

## Configuration

| Key | Environment variable | Default | Meaning |
|---|---|---|---|
| `CareLoop:FhirBaseUrl` | `CareLoop__FhirBaseUrl` | `http://localhost:5080/fhir` | Base URL of CareLoop's FHIR R4 endpoints, used to read case status. |
| `CareLoop:NotificationCallbackUrl` | `CareLoop__NotificationCallbackUrl` | `http://localhost:5080/notifications/inbound/simulator` | Callback of CareLoop's simulator notification provider. Delivery reports and patient responses are posted here. |
| `CareLoop:InboundSecret` | `CareLoop__InboundSecret` | empty | Secret every callback is signed with. Must equal CareLoop's `Notifications:Simulator:InboundSecret`. Docker Compose passes `SIMULATOR_INBOUND_SECRET` to both. While it is empty nothing is sent and an inbound request answers 502. |
| `CareLoop:SendAttempts` | `CareLoop__SendAttempts` | `3` | How many times a callback is sent in total when CareLoop answers with a server error or does not answer in time. A refusal (4xx) is never retried. |
| `CareLoop:RetryDelayMilliseconds` | `CareLoop__RetryDelayMilliseconds` | `200` | Pause before each further attempt, multiplied by the attempts already made. |
| `CareLoop:TimeoutSeconds` | `CareLoop__TimeoutSeconds` | `10` | How long one call to CareLoop may take before it counts as not answered. All attempts together must finish inside CareLoop's own HTTP timeout, because the delivery report is sent while CareLoop is still waiting for the answer to its `CommunicationRequest`. |
| `PatientChannel:DefaultChannel` | `PatientChannel__DefaultChannel` | `App` | Inbox for a patient message whose inbox address names no channel. An address starting `SYN-SMS-` goes to the text-message inbox and `SYN-PORTAL-` to the portal; a caregiver message always goes to email. |
| `Scenarios:Directory` | `Scenarios__Directory` | `../../scenarios` (relative to the content root; `/app/scenarios` in Docker) | Folder holding the shared scenario JSON files, `src/simulators/scenarios`. |
| `Scenarios:RealSecondsPerScenarioHour` | `Scenarios__RealSecondsPerScenarioHour` | `2` | Pacing of a scenario run: real seconds per scenario hour. A scenario patient's `respondAfter` is compressed by it when a run starts. It must match the factor CareLoop's demo pathway was shortened by (1800, which is 2 seconds per hour) and the other simulator's setting. |
| | `ASPNETCORE_URLS` | `http://localhost:5082` (launch profile); `http://+:8080` in Docker | Listen address. |

The only secret is the callback signing secret. It is a local development value for synthetic
data: copy `.env.example` to `.env` before `docker compose up`, or set
`Notifications__Simulator__InboundSecret` on the API and `CareLoop__InboundSecret` here to the
same value when running with `dotnet run`. Never commit a real one.

## Routes

The `/api` and `/scenarios` routes answer errors as problem details (`title`, `detail`); the
`/fhir` route answers them as a FHIR `OperationOutcome`.

| Method | Route | Purpose |
|---|---|---|
| GET | `/health` | Liveness: `{ "status": "ok", "service": "patient-channel-simulator", "synthetic": true }`. |
| GET | `/openapi/v1.json` | OpenAPI document. |
| POST | `/fhir/{resourceType}` | FHIR inbound from CareLoop; `CommunicationRequest` lands in the patient inbox. 201 for a new request, 200 for a resend, 400 for anything else or an unreadable body, 502 when the delivery outcome could not be reported (a resend retries it). |
| GET | `/api/inbox` | List simulated inbox messages. |
| GET | `/api/inbox/{messageId}` | One inbox message. |
| POST | `/api/inbox/{messageId}/acknowledge` | Patient acknowledges ("I will come") → acknowledgement `Communication` and nothing else. Every action answers 404 for an unknown message, 409 for a caregiver's copy or a message that did not reach the patient, and 502 when CareLoop refused or could not be reached. |
| POST | `/api/inbox/{messageId}/understand` | Patient teach-back ("I understand") → teach-back `Communication`. |
| POST | `/api/inbox/{messageId}/call-me` | Patient asks for a call → call-me `Communication`. |
| POST | `/api/inbox/{messageId}/reply` | Body `{ "text": "1" }`. A text reply to an SMS message: `1` acknowledges, `2` asks for a call; other text is 400 and a non-SMS message 409 (C7). |
| POST | `/api/inbox/{messageId}/rate-confidence` | Body `{ "rating": 4 }`. A 1 to 5 confidence rating → confidence-rating `Communication`; outside 1 to 5 is 400 (C10). |
| GET | `/api/patient-behaviours` | Patients that have a behaviour of their own. |
| GET | `/api/patient-behaviours/default` | The behaviour used for every other patient. |
| PUT | `/api/patient-behaviours/default` | Change the default behaviour. |
| PUT | `/api/patient-behaviours/{patientSyntheticId}` | Set how one patient reacts. 400 when the identifier is not synthetic or the behaviour breaks a rule. |
| DELETE | `/api/patient-behaviours/{patientSyntheticId}` | Remove a patient's behaviour; the default applies again. |
| GET | `/api/fhir-log` | FHIR message log. |
| GET | `/api/case-status/{taskId}` | Patient case status from the CareLoop case `Task`. |
| GET | `/scenarios` | List scenarios. |
| POST | `/scenarios/{name}/run` | Put the scenario's patient behaviours in force. Optional body `{ "realSecondsPerScenarioHour": 2 }`. 202 with the run, 404 for an unknown scenario, 400 for a cohort. |
| GET | `/scenarios/runs/{runId}` | The run's patients and how they have reacted so far. |

ClientApp routes (`/`, `/case-status`, `/patient-behaviours`, `/runner`, `/message-log`) are
handled by the single-page-app fallback and must not start with `/api`, `/fhir`, `/scenarios` or
`/health`.

## Open points

- **Text-message inbox.** CareLoop sends no channel, so a patient message is shown as a text
  message only when its inbox address starts with `SYN-SMS-`. That convention is a WS8 proposal
  (see `docs/workstreams.md`, "Contact point channel") and no scenario uses it yet.
- **Full journey.** The HIS simulator (WS7) is not built, so a notification from a running
  CareLoop has to be raised by posting FHIR by hand: a `DiagnosticReport` and then a completed
  clinical-review `Task`, each with `meta.source` `his-simulator`
  (samples in `tests/api/CareLoop.Tests/Api/Fhir/Samples`).
- **FHIR version.** R4 (Firely `Hl7.Fhir.R4`) is still an assumption to be confirmed.
