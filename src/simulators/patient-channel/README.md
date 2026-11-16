# SMS and email simulator

> **Simulated data only.** Every patient, caregiver, staff address, message and case handled here is
> synthetic. Addresses are prefixed `SYN-`, names start with "Synthetic", and every record carries a
> `simulated` marker. No text message or email is really sent. Never point this service at a live endpoint.

A stand-in for an SMS and email service, used to demonstrate CareLoop (workstream **WS8**; paths
below are relative to the repository root). The folder and project keep the name `patient-channel`;
renaming them is a separate change.

CareLoop reaches patients, caregivers and staff by **text message and email only**. There is no
health app and no patient portal. CareLoop's `Sms` and `Email` notification providers post each
message here as plain JSON, and the simulator calls back with delivery reports and replies, the way
an SMS or email service would. **No FHIR crosses this simulator.** It shares no code with `src/api/`
or the HIS simulator.

**Status: built.** Every route answers, the ClientApp has four screens, and no `TODO(WS-8)`,
skipped test or `it.todo` remains.

Screen designs are screens 9, 10 and 12 in [docs/ui-design](../../../docs/ui-design/README.md).

## What it does

| Folder | Direction | Purpose |
|---|---|---|
| `Inbound/` | CareLoop → simulator | Receives a text message or an email (`POST /messages`) into a simulated **inbox**; simulates the delivery outcome; the routes the UI uses to list messages and reply as the patient. |
| `Outbound/` | simulator → CareLoop | Posts delivery reports and replies to CareLoop's provider callback, `POST {CallbackBaseUrl}/sms` or `/email`. Every post is signed. It reads nothing from CareLoop. |
| `Scenarios/` | | Loads the shared scenario files and, when a run starts, puts each scenario patient's `behaviour` in force. It performs no steps and sends nothing by itself. |
| `State/` | | In-memory inboxes, message log, scenario runs, per-patient behaviours, the record of every reaction and the replies waiting for their due time. Lost on restart by design. |
| `ClientApp/` | | React UI: inboxes (patient text messages, patient email, caregiver, staff), patient behaviours, scenario runner, message log. |

### The wire format

CareLoop sends a message:

```http
POST /messages
Content-Type: application/json

{ "messageId": "<notification attempt id>", "channel": "sms", "to": "SYN-PT-0001", "subject": null,
  "body": "<rendered text>", "language": "en",
  "metadata": { "caseId": "<case id>", "audience": "patient" } }
```

`channel` is `sms` or `email`; `subject` is kept for email only. `to` must start with `SYN-`.
`metadata` is returned unchanged on every callback; the simulator reads only `caseId` and
`audience` (`patient`, `caregiver` or `staff`; a message that names none is a patient's). The answer
is 201 for a new message and 200 for a resend of the same `messageId`.

The simulator calls back:

```json
{ "type": "delivery", "messageId": "…", "status": "delivered", "at": "…", "metadata": { … } }
{ "type": "delivery", "messageId": "…", "status": "failed", "reason": "…", "at": "…", "metadata": { … } }
{ "type": "reply", "replyId": "<messageId>-r1", "inReplyTo": "…", "from": "SYN-PT-0001", "text": "1", "at": "…", "metadata": { … } }
```

Safety meaning carried by the design (see the XML documentation for detail):

- **Delivery is not acknowledgement.** A delivered message stays unacknowledged until the patient
  replies; a delivery report says nothing about a reply.
- **The simulator never rewrites content.** It shows exactly what CareLoop rendered.
- **A reply is forwarded as typed.** The simulator does not say what a reply means and refuses none
  for its wording. CareLoop decides: `1` is "I will come" (the acknowledgement), and `2` or any
  other text asks for a call.
- **Only the patient replies.** A caregiver's message and a staff notice are shown, get a delivery
  report and can never be replied to.
- **No teach-back and no rating here.** Whether the patient understood, and how confident they
  feel, are recorded by a nurse in CareLoop after a call, not by anything in this simulator.
- **Callbacks are signed, or not sent.** Every callback carries
  `X-CareLoop-Signature: sha256=<hex HMAC-SHA256 of the body>`. With no secret configured the
  simulator sends nothing and says which setting is missing.
- **The simulator plays the patient by reacting.** Each patient has a behaviour: what the channel
  reports (delivered, failed, or nothing) and what the patient then does (nothing, reply 1, or
  reply 2), optionally after a delay or only on a reminder. It is applied when a message arrives.
  If CareLoop sends nothing, the patient does nothing, so a test cannot pass on a notification
  that was never sent.
- **Behaviour is per patient, found by address.** A behaviour is looked up by the address a
  message was sent to. A patient with none gets the default, which is deliver and do not reply. A
  caregiver's or staff message is simply delivered unless a behaviour was set for that address.
- **Time comes from `ISimulatorClock`**, never from the system clock directly.

## Run

Prerequisites: .NET SDK 10, Node 22 (>= 22.22.2) with corepack (do not run `corepack enable`;
invoke Yarn as `corepack yarn`).

```bash
# .NET host on http://localhost:5082
dotnet run --project src/simulators/patient-channel/PatientChannelSimulator

# ClientApp dev server (proxies /api, /messages, /scenarios, /health, /openapi to :5082)
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
| `CareLoop:CallbackBaseUrl` | `CareLoop__CallbackBaseUrl` | `http://localhost:5080/notifications/inbound` | CareLoop's provider callback, without the channel. Delivery reports and replies go to `{CallbackBaseUrl}/sms` or `{CallbackBaseUrl}/email`. |
| `CareLoop:InboundSecret` | `CareLoop__InboundSecret` | empty | Secret every callback is signed with. Must equal CareLoop's `Notifications:Sms:InboundSecret` and `Notifications:Email:InboundSecret`. While it is empty nothing is sent and an inbound message answers 502. |
| `CareLoop:SendAttempts` | `CareLoop__SendAttempts` | `3` | How many times a callback is sent in total when CareLoop answers with a server error or does not answer in time. A refusal (4xx) is not sent again within the same attempt. |
| `CareLoop:RetryDelayMilliseconds` | `CareLoop__RetryDelayMilliseconds` | `200` | Pause before each further attempt, multiplied by the attempts already made. |
| `CareLoop:DeliveryReportRetries` | `CareLoop__DeliveryReportRetries` | `10` | How many more times a delivery report is tried in the background after it failed while the notification was being received. CareLoop does not send a patient notification again, so without this a failed report would leave the message undelivered for good. `0` switches the retries off. |
| `CareLoop:DeliveryReportRetrySeconds` | `CareLoop__DeliveryReportRetrySeconds` | `5` | Wait before the first background retry. Each further retry waits twice as long. |
| `CareLoop:DeliveryReportRetryMaximumSeconds` | `CareLoop__DeliveryReportRetryMaximumSeconds` | `60` | The longest wait between two background retries. |
| `CareLoop:TimeoutSeconds` | `CareLoop__TimeoutSeconds` | `10` | How long one call to CareLoop may take before it counts as not answered. All attempts together must finish inside CareLoop's own HTTP timeout, because the delivery report is sent while CareLoop is still waiting for the answer to the message it posted. |
| `Scenarios:Directory` | `Scenarios__Directory` | `../../scenarios` (relative to the content root; `/app/scenarios` in Docker) | Folder holding the shared scenario JSON files, `src/simulators/scenarios`. |
| `Scenarios:RealSecondsPerScenarioHour` | `Scenarios__RealSecondsPerScenarioHour` | `2` | Pacing of a scenario run: real seconds per scenario hour. A scenario patient's `respondAfter` is compressed by it when a run starts. It must match the factor CareLoop's demo pathway was shortened by (1800, which is 2 seconds per hour) and the other simulator's setting. |
| | `ASPNETCORE_URLS` | `http://localhost:5082` (launch profile); `http://+:8080` in Docker | Listen address. |

The only secret is the callback signing secret. It is a local development value for synthetic
data: set `Notifications__Sms__InboundSecret` and `Notifications__Email__InboundSecret` on the API
and `CareLoop__InboundSecret` here to the same value. Never commit a real one.

## Routes

Errors are answered as problem details (`title`, `detail`).

| Method | Route | Purpose |
|---|---|---|
| GET | `/health` | Liveness: `{ "status": "ok", "service": "patient-channel-simulator", "synthetic": true }`. |
| GET | `/openapi/v1.json` | OpenAPI document. |
| POST | `/messages` | A text message or an email from CareLoop; it lands in the inbox. 201 for a new message, 200 for a resend, 400 for an unusable body, an unknown channel or audience, or an address that is not synthetic (logged without its body). When the delivery outcome cannot be reported the message is still accepted, stays pending and the report is retried in the background; 502 only when it cannot be reported at all (no signing secret, or retries switched off). |
| GET | `/api/inbox` | List every simulated message, newest first. |
| GET | `/api/inbox/{messageId}` | One message. |
| POST | `/api/inbox/{messageId}/reply` | Body `{ "text": "1" }`. The patient replies to a text message or an email; the text is sent to CareLoop as typed. 400 for an empty reply, 404 for an unknown message, 409 for a caregiver's or staff message or one that did not reach the patient, 502 when CareLoop refused or could not be reached. |
| GET | `/api/patient-behaviours` | Patients that have a behaviour of their own. |
| GET | `/api/patient-behaviours/default` | The behaviour used for every other patient. |
| PUT | `/api/patient-behaviours/default` | Change the default behaviour. |
| PUT | `/api/patient-behaviours/{patientSyntheticId}` | Set how one patient reacts. 400 when the identifier is not synthetic or the behaviour breaks a rule. |
| DELETE | `/api/patient-behaviours/{patientSyntheticId}` | Remove a patient's behaviour; the default applies again. |
| GET | `/api/message-log` | Message log: every exchange with CareLoop, newest first. |
| GET | `/scenarios` | List scenarios. A file that cannot be used is still listed, with the reason in `problem`, and cannot be run. |
| POST | `/scenarios/{name}/run` | Put the scenario's patient behaviours in force. Optional body `{ "realSecondsPerScenarioHour": 2 }`. 202 with the run, 404 for an unknown scenario, 400 for a cohort. |
| GET | `/scenarios/runs/{runId}` | The run's patients and how they have reacted so far. |

ClientApp routes (`/`, `/patient-behaviours`, `/runner`, `/message-log`) are handled by the
single-page-app fallback and must not start with `/api`, `/messages`, `/scenarios` or `/health`.

## Open points

- **Patient addresses come from the patient's synthetic id.** Scenario files carry no phone
  number or email address yet, so the HIS simulator sends each patient's synthetic id as their
  address and a scenario's behaviour is found by it. Adding contacts to the scenario schema is
  WS10 (see `docs/workstreams.md`).
- **Scenario vocabulary.** The shared schema still allows the responses `understand` and
  `rate-confidence` and a `confidence` value. This simulator refuses a scenario that uses them,
  because a patient cannot do either by text or email. No shipped scenario uses them; removing
  them from the schema is WS10.
- **Project name.** The folder, project, Docker service and health name are still
  `patient-channel`.
