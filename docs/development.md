# Development guide

## Prerequisites and first run

- .NET 10 SDK (the repository pins the .NET 10 SDK feature band in `global.json`).
- Node.js 24 and npm for the web app.
- Docker Compose v2 for the local container setup.

Run `dotnet build CareLoop.sln` and `dotnet test CareLoop.sln` for the API, the Domain and both simulators; the build fails on any warning, including a missing XML documentation comment. Run `dotnet run --project src/api/CareLoop.Api` to start the API on `http://localhost:5080`; its routes and configuration are in `src/api/README.md`, and each simulator's README covers its own host and ClientApp. In `src/web`, run `npm ci`, then `npm run dev`. The staff dashboard proxies `/api` to the API; it is not a patient-facing product. Run `npm run test` and `npm run build` for unit tests, documentation lint, strict type-checking and the production build. Regenerate API types with `npm run generate:api` after building the API, and commit them with the contract: CI fails when they no longer match `contracts/openapi/careloop.json`.

`docker compose up --build` runs PostgreSQL on 5432, the API on port 8080, the dashboard on port 4173, the HIS simulator on 5081 and the messaging simulator on 5082 (ports come from `.env`, template in `.env.example`). nginx proxies the dashboard's `/api/` requests to the API. No production deployment environment is configured.

AI is off by default (`Ai:Provider` = `NoOp`), and everything runs without it. With `Ai:Provider` = `Foundry` the API asks the agents in a Microsoft Foundry project (`Ai:Foundry:ProjectEndpoint`; agent names in `Ai:Foundry:Agents`) for three texts: the role briefing, the impact summary and the patient message draft. Each text has its own agent, owned in the Foundry project; the API only calls them. The instructions for a provider that takes a prompt are the plain-text files under `src/api/CareLoop.Api/Prompts/`; on Foundry the role briefing's file applies, the impact summary's agent follows its own instructions, and the patient message draft is one of its agent's own templates, filled in. There is no separate AI service and no key: the API signs in with `az login` locally. A container has no `az login`, so a live check uses `dotnet run`; the commands are in `src/api/README.md`. The contract is `docs/ai-design/integration.md`.

## Working agreements

- Work in the component folders shown in the root README and keep cross-component interfaces in `contracts/`. File ownership per workstream is in `docs/workstreams.md`; the rules every contributor follows are in `AGENTS.md`.
- Agree shared event schemas and state-transition rules before changing their shape. Version intentional breaking changes and update all affected producers, consumers, and fixtures together.
- Keep the UI and simulators independently runnable where practical. Dashboard tests may intercept API calls with synthetic fixtures; the dashboard itself reads the API and has no synthetic-data branches or simulator-store coupling.
- HIS simulator validation: `dotnet test tests/simulators/his/HisSimulator.Tests/HisSimulator.Tests.csproj`; in its `ClientApp`, `corepack yarn test` and `corepack yarn build`. Node 24+ and Yarn 4.18.1 are required. Backend HTTP tests run Kestrel against a fake upstream; UI tests intercept synthetic fetches. These are not WS10's full patient journeys. Headless runs use `POST /scenarios/{name}/run` without a UI or extra flag.
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
- CareLoop reaches patients, caregivers and staff by text message and email only; there is no patient app or portal. Do not put sensitive clinical details in SMS or email content: neither carries a result, and a person gives it. Treat all generated patient-facing content as a draft until approved.
- Enforce demo roles at the API boundary, not just by hiding UI controls.
- Simulators label their synthetic records and messages. The API and staff dashboard carry no simulated-data flags or labels. Keep real-world impact claims out of demo results.
- Database and hosting choices are not set by this bootstrap. Record the team's decision and a reproducible local setup before adding environment-specific files.

## Pull request checklist

- [ ] Linked user stories and FRs are identified; scope stays within the approved synthetic prototype.
- [ ] Shared contracts and affected workstreams are updated together.
- [ ] Authorization, source provenance, idempotency, ownership, audit, and error behavior are covered where applicable.
- [ ] Tests cover the introduced behavior and relevant failure/duplicate path.
- [ ] No real patient data, secret, live endpoint, or unapproved clinical rule was introduced.
- [ ] Demo behavior distinguishes delivery, acknowledgement, understanding, consent, referral acceptance, attendance, care, and closure.
- [ ] Documentation and simulator-owned synthetic demo labels are current.
