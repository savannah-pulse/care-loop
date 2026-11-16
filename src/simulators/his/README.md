# HIS simulator

WS7's independently runnable hospital-system stand-in. Synthetic data only: patient/staff identifiers
are `SYN-` prefixed, patient names are visibly synthetic, and outgoing FHIR carries a simulator-owned
marker. CareLoop's API and dashboard do not depend on that marker.

The simulator shares no code with CareLoop and calls it **only through `/fhir`**. It supplies source
facts and human decisions; it never selects urgency, evidence kinds, closure reasons or clinical deadlines.
AI approval stays in the CareLoop dashboard. This simulator receives approved `DocumentReference`s only.

## Run

From the repository root, with .NET 10:

```powershell
dotnet run --project src\simulators\his\HisSimulator
```

API: `http://localhost:5081`; OpenAPI: `/openapi/v1.json`.

The optional ClientApp needs Node 24+ and Yarn 4.18.1:

```powershell
Set-Location src\simulators\his\HisSimulator\ClientApp
corepack yarn install --immutable
corepack yarn dev
```

UI: `http://localhost:5181`; Vite proxies local API routes to port 5081. Hash routing avoids
API/UI route collisions. If Corepack is unavailable, use `npx --yes @yarnpkg/cli-dist@4.18.1`
instead of `corepack yarn`; no global activation is necessary. Respect your environment's configured
package registry/proxy rather than disabling TLS verification.

For a single host, build the ClientApp and copy its `dist` contents into the project's git-ignored
`wwwroot`. Docker performs that copy:

```powershell
docker compose up --build his-simulator
```

## Headless replay and demo boundaries

No UI or headless flag is required. `POST /scenarios/{name}/run` starts supervised background work,
returns **202** with a progress `Location`, and continues after the initiating request ends:

```powershell
$run = Invoke-RestMethod http://localhost:5081/scenarios/silent-patient/run `
  -Method Post -ContentType application/json -Body '{"runKey":"team-demo"}'
Invoke-RestMethod ("http://localhost:5081/scenarios/runs/" + $run.runId)
```

- Only HIS events are replayed. Patient delivery/replies remain WS8.
- Source IDs/versions are deterministic from the run key and step id. Reusing a key intentionally
  deduplicates in CareLoop; simultaneous runs of the same key are refused with 409.
- Steps use real send timestamps. Offsets are paced by real seconds per scenario hour; CareLoop's
  clock is never accelerated. Zero pacing is supported for tests or runs that do not measure deadlines.
- Each 4xx is recorded as `rejected`; replay can finish with rejections. Transport/server errors and
  any unexpected exception stop the run as `failed`, with the step marked failed and its message kept.
  Host shutdown cancels pending work and records failure.
- `completed` means the HIS replay finished, **not** that expected clinical/coordination outcomes occurred.
- Cohorts return 422 before starting work; expansion belongs to WS10.
- Shared scenarios are unchanged. `happy-path.json` still has an unsupported evidence placeholder.
  Consent is available as a manual action and local runner action `consent-recorded`; adding it to the
  shared schema and shared scenarios remains WS10.
- Full screening-to-care E2E journeys require WS8/WS10. Do not present these tests as those journeys.

## FHIR contract

Every outgoing resource names `meta.source = his-simulator`. Versioned source states keep stable
resource IDs. Creation uses POST; updates use `PUT /fhir/{type}/{id}`.

| Source event | Carrier |
|---|---|
| Screening | Final `DiagnosticReport`, contained `Patient` and `Observation`; name/MRN/language and one `telecom` contact, `system=sms` by default or `email` (`Contacts:PatientChannel`), whose value is the patient's synthetic id |
| Clinical review | Completed `Task` with `reviewed-urgency` extension; source urgency passed through unchanged |
| Consultation/referral | `ServiceRequest` first, then a stable `Task`; subsequent state changes update the Task |
| Appointment | `Appointment` status and supplied time; optional synthetic `Location` participant |
| Outcome | `Procedure` or follow-up `DiagnosticReport`, code system `https://careloop.invalid/fhir/CodeSystem/evidence-kind` |
| Closure | One completed **actual CareLoop case Task**, containing matching human `Provenance` |
| Caregiver consent | `Consent`, report-correlation extension, contained `RelatedPerson` on a grant, `telecom.system=email` |

Successful partial sends persist their resource references, so retrying a refused Task does not
resend its accepted dependency. Closure sends one carrier: `relevantHistory` references its contained
Provenance, whose target is `#`. It includes only evidence/exception/date/attempt references explicitly
supplied by the human or scenario.

CareLoop's actual case Task ID is learned from inbound FHIR: a case Task delivered to the inbox, or the
case Task that a notice focuses, read back with GET `/fhir/Task/{id}`. Early delivery before initial
screening storage is retained. A notice is filed in the inbox **first** and the read-back is best
effort: if CareLoop answers 404, 5xx or cannot be reached, the notice is still accepted (201/200) and
only the case Task ID stays unknown. No REST search or guessed case ID is used, so closure keeps
failing explicitly (409) until the real Task ID is known from a later notice or case Task. Closure
needs that ID because it must target CareLoop's actual case Task.

Closure sends the kebab-case wire code in `Task.statusReason`. `closureReason` accepts the six reasons
by name (`Treated`, `OnSurveillance`, `NegativeOnConfirmation`, `Declined`, `TransferredAndAccepted`,
`Untraceable`; any case) or by code (`treated`, `on-surveillance`, `negative-on-confirmation`,
`declined`, `transferred-and-accepted`, `untraceable`); anything else is refused with 400. A next
surveillance date is exactly `yyyy-MM-dd`. `evidenceResourceKey` must name an outcome resource
(a `Procedure` or follow-up `DiagnosticReport` the simulator sent), else 400.

Consultation requests name a `Task.owner` only when a `proposedOwner` (with a facility) is supplied,
because CareLoop reads a requested consultation Task with an owner as a handoff proposal to that
person. A referral is never `completed` (CareLoop ignores it): only `requested`, `accepted` and
`rejected` are sent. A referral answer is sent alone, never preceded by a made-up request; only a
`requested` referral sends its `ServiceRequest` first. `POST /actions/referral-decision` requires
`accepted` (400 if absent), so the simulator never sends a decision nobody stated.

Inbox items: a case Task with status `requested` is a handoff proposal, with status `rejected` a
handoff rejection, and an in-progress one carrying a `rejected` status reason a referral rejection.

Memory is bounded: the FHIR log, the staff inbox and the scenario runs each keep the newest 1000
entries (`SimulatorStore.DefaultCapacity`; the oldest are dropped, and a run still in progress is
never dropped). `GET /fhir-log` accepts `limit` (newest N) and `since` (ISO 8601 instant; only newer
entries) so a poller need not read everything; `GET /fhir-log/{id}` reads one entry. The UI asks
for `limit=200`.

Inbox resource replay replaces one deterministic inbox item; every exchange remains in the wire log.
Direct staff notifications ("a case needs your action") do **not** arrive here: CareLoop sends them
by text message or email, and the SMS and email simulator shows them. The staff inbox holds what
CareLoop tells the hospital system as FHIR: escalations, handoffs, call requests and handoff
summaries. A `CommunicationRequest` is refused (422), and any other Task is filed as "Other case
update". There is **no** `POST /staff-inbox/notifications`.
Receipt and opening an item never accept ownership or acknowledge a notification.

## Routes

| Method | Route | Result |
|---|---|---|
| GET | `/health` | 200 liveness |
| GET | `/openapi/v1.json` | Simulator contract |
| POST | `/fhir/{resourceType}` | 201 raw FHIR and Location |
| PUT | `/fhir/{resourceType}/{id}` | 200 raw FHIR; URL/body IDs must match |
| GET | `/staff-inbox?recipient=...`, `/staff-inbox/{id}` | Filtered inbox or detail |
| GET | `/fhir-log?direction=sent\|received&caseRef=...&since=...&limit=...`, `/fhir-log/{id}` | Exact request/response bodies, including refusals; newest first |
| GET | `/cases` | Source-local cases (with patient name and MRN), successfully sent references and the approved handoff summary once received |
| GET | `/scenarios`, `/scenarios/runs/{runId}` | Summaries and progress |
| POST | `/scenarios/{name}/run` | 202 supervised replay |
| POST | `/actions/clinical-review` | Source urgency |
| POST | `/actions/consultation-request`, `/actions/consultation-answer` | Propose or answer a handoff |
| POST | `/actions/referral`, `/actions/referral-decision` | Source state or acceptance/rejection |
| POST | `/actions/appointment` | Booked, fulfilled or noshow |
| POST | `/actions/outcome` | Finalized source outcome |
| POST | `/actions/closure` | Human authorization and supplied evidence/exception |
| POST | `/actions/consent` | Active, inactive or rejected consent |

Manual actions return 200 with `accepted` and each upstream status. An upstream clinical refusal is
`accepted:false`, not simulator success. Local invalid input/unknown cases/conflicts return Problem
Details with 400/404/409; unsupported operations return 422; transport/timeouts return 502/504.
The [.http harness](HisSimulator/HisSimulator.http) covers each route.

## Configuration

| Key | Default | Meaning |
|---|---|---|
| `CareLoop:FhirBaseUrl` | `http://localhost:5080/fhir` | The only CareLoop address used |
| `Contacts:PatientChannel` | `sms` | How patients are reached: `sms` or `email`. CareLoop reaches people by text message and email only, so no other value is accepted. Scenario files carry no phone number or email address yet (WS10), so the patient's synthetic id is sent as the address |
| `Scenarios:Directory` | `../../scenarios` | Relative to host content root; `/app/scenarios` in Docker |
| `Scenarios:RealSecondsPerScenarioHour` | `2` | Must match demo pathways' factor 1800 and WS8 pacing |
| `ASPNETCORE_URLS` | Launch profile 5081 / Docker 8080 | Host listen address |

Use double underscores for nested environment keys, e.g. `CareLoop__FhirBaseUrl`.
In-memory cases/inbox/runs/logs disappear on restart. No real integrations, secrets or patient data
are required. FHIR R4 matches the existing prototype contract; the deployment exchange version
and clinical approval of pathway placeholders remain open.

## Test and team demonstration

```powershell
dotnet test tests\simulators\his\HisSimulator.Tests\HisSimulator.Tests.csproj
Set-Location src\simulators\his\HisSimulator\ClientApp
corepack yarn test
corepack yarn build
corepack yarn format:check
```

The backend suite covers builder wire shapes, HTTP logging/refusals, ordered/partial/concurrent sends,
store snapshots, scenario validation/pacing/shutdown, and **real loopback Kestrel HTTP endpoints**.
Those HTTP tests use a capturing fake CareLoop HTTP handler, not a real API/database.
The UI suite covers explicit human submission, exact evidence, upstream refusal vs local errors,
recipient/direction filters, cancellation, scenario progress and synthetic labelling.

For the team, run these suites first, then start the API/simulator and demonstrate:
headless run progress; source actions; inbound notices without automatic acceptance; exact FHIR
request/response inspection. Supply clinical codes from the configured pathway rather than inventing
them. Real patient response, approved AI summary delivery and asserted full journeys remain separate
WS8/WS9/WS10 demonstrations.
