# CareLoop API

The coordination engine: ASP.NET Core minimal APIs on .NET 10, EF Core on PostgreSQL, FHIR R4 through the Firely SDK.
It is not a clinical system. It is built as for production and is currently connected to the two simulators. Read the root `AGENTS.md` before changing anything. Paths below are relative to the repository root.

This is a scaffold. Contracts, signatures and documentation are complete; behaviour is stubbed.
Every stub is tagged `// TODO(WS-n): ...` and throws `NotImplementedException("WSn: Class.Method")`, and every endpoint
except `/health` answers **501** with the owning workstream. See `docs/workstreams.md` for who owns what.

## Projects

| Project | What it is | References |
|---|---|---|
| `src/api/CareLoop.Domain` | Cases, tracks, rules, pathway definition, domain events | nothing |
| `src/api/CareLoop.Api` | Contracts, feature slices, infrastructure, background workers | Domain |
| `tests/api/CareLoop.Tests` | xUnit tests, fakes and synthetic FHIR samples | Domain, Api |

## Run

```bash
dotnet run --project src/api/CareLoop.Api
```

The API listens on `http://localhost:5080`. `GET /health` returns 200. The OpenAPI document is served at
`/openapi/v1.json` and written to `contracts/openapi/careloop.json` on every build.

A database is only needed once persistence is implemented. Start one with `docker compose up db` from the repository root.

## Test

The root solution also contains the two simulators; to build and test only the API:

```bash
dotnet build src/api/CareLoop.Api && dotnet build tests/api/CareLoop.Tests
```

```bash
dotnet test tests/api/CareLoop.Tests
```

Or everything at once with `dotnet build CareLoop.sln` and `dotnet test CareLoop.sln`.

The build treats every warning as an error, including a missing XML documentation comment (`CS1591`).
Planned behaviours exist as named, skipped tests (`[Fact(Skip = "WSn: not implemented")]`). Un-skip a test when you
implement its behaviour; do not weaken or delete one to make it pass.

## Database migrations

The EF Core tool is pinned in `.config/dotnet-tools.json` at the repository root.

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add <Name> --project src/api/CareLoop.Api --output-dir Infrastructure/Persistence/Migrations
```

Migrations are applied at startup only when `Database:MigrateOnStartup` is `true` (Docker Compose sets it).

## Configuration

The base `appsettings.json` configures no external system and no simulator address. `appsettings.Development.json`,
used by `dotnet run`, adds the HIS simulator as the one external system and points the simulator notification provider
at both simulators; Docker Compose sets the same through environment variables. Production supplies its own entries.

| Key | Default | Meaning |
|---|---|---|
| `ConnectionStrings:CareLoop` | local PostgreSQL | Database connection. Override with the environment variable `ConnectionStrings__CareLoop`. |
| `Database:MigrateOnStartup` | `false` | Apply migrations when the API starts. |
| `Pathways:Directory` | `Infrastructure/Pathways` | Folder the pathway definitions are loaded from. Development and Docker Compose use `Infrastructure/Pathways/demo`, which has the same rules with deadlines shortened for live demos. |
| `ExternalSystems:Systems:{systemId}:BaseUrl` | none | Address of one system of record (HIS, EMR, lab system). Any number may be configured. The key must equal the source system identifier that system sends; messages about a case go back to the system the case came from. |
| `Notifications:Simulator:PatientChannelBaseUrl` | none | Where the simulator provider sends patient messages (the patient-channel simulator). |
| `Notifications:Simulator:StaffInboxBaseUrl` | none | Where the simulator provider sends staff messages (the HIS simulator's staff inbox). |
| `Notifications:EnabledProviders` | `[ "Simulator" ]` | Enabled notification providers. SMS and email are stubs and must stay off. |
| `Notifications:PerEventOverrides` | `{ }` | Optional provider list per domain event name. |
| `Outbox:Enabled` | `false` | Run the outbox dispatcher. Off until WS4 implements it. |
| `DeadlineWorker:Enabled` | `false` | Run the deadline worker. Off until WS5 implements it. |
| `Ai:Provider` | `NoOp` | `NoOp` (AI off) or `AzureOpenAi`. Azure settings come from environment variables only. |

Clinical deadlines are **not** configuration keys here. They live in
`src/api/CareLoop.Api/Infrastructure/Pathways/breast-screening.json`, where every value is a placeholder.

The API always runs on real time. For a live demo it reads `Pathways/demo/breast-screening.json` instead: the same
definition with every duration divided by 1800, so a one-day deadline passes in 48 seconds. `DemoPathwayFileTests`
fails if the two files differ in anything but that factor. Never point a real deployment at the demo folder.

## Routes

| Method | Route | Owner |
|---|---|---|
| GET | `/health` | works |
| POST | `/fhir/{resourceType}` | WS3 |
| PUT | `/fhir/{resourceType}/{id}` | WS3 |
| GET | `/fhir/Task/{id}` | WS3 |
| GET | `/fhir/metadata` | WS3 |
| GET | `/cases` | WS6 |
| GET | `/cases/{caseId}` | WS6 |
| GET | `/cases/{caseId}/timeline` | WS6 |
| GET | `/exceptions` | WS6 |
| GET | `/metrics` | WS6 |
| POST | `/metrics/simulation` | WS10 |
| POST | `/cases/{caseId}/handoff-summary` | WS9 |
| POST | `/cases/{caseId}/handoff-summary/{summaryId}/approval` | WS9 |

External systems use `/fhir/...` only. The plain REST routes are for the CareLoop dashboard.
