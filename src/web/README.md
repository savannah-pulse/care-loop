# CareLoop web

CareLoop's own dashboard for staff: the worklist, a case, escalations, the AI handoff summary, impact and programmes (screens 1, 2, 17, 18, 3 and 4 in [docs/ui-design](../../docs/ui-design/README.md)). React 19, strict TypeScript and Vite. It reads and writes through the CareLoop REST API only, never a simulator's store, and makes no clinical decisions; the hospital-system and patient-channel screens belong to the simulators.

## Run it

From this folder:

```powershell
npm ci
npm run dev                                                       # http://localhost:5173, /api forwarded to http://localhost:5080
$env:CARELOOP_API_URL='http://localhost:8080'; npm run dev        # the API that Docker Compose publishes
$env:CARELOOP_MOCK_API='1'; npm run dev                           # local data, no API needed
```

In bash, use `CARELOOP_API_URL=http://localhost:8080 npm run dev` or `CARELOOP_MOCK_API=1 npm run dev`. The same works for `npm run preview` after `npm run build`.

- **Against the API.** The dev server forwards `/api/*`, without the `/api` prefix, to `CARELOOP_API_URL`: `http://localhost:5080` by default, where `dotnet run --project src/api/CareLoop.Api` starts the API. Docker Compose publishes the API on `http://localhost:8080`. The API has no CORS policy, so the browser always calls `/api` on the dashboard's own origin. In the container, nginx forwards `/api/` to the `api` service, and the dashboard is on <http://localhost:4173>; start everything with `docker compose up --build` at the repository root.
- **Without the API.** `CARELOOP_MOCK_API=1` answers `/api` from `mock/`, typed against the generated API types, with the API's read rules: identity headers, the reader's facility, a reason for other facilities, the coordinator's narrower view without names, the clinician-only patient search, paging limits and who may acknowledge. It also answers the handoff summary and the comparison, which the API does not build yet (501), so those screens can be worked on; it returns the same measures for every facility. It runs only inside `vite` and `vite preview`; the app does not know it exists and the build never contains it. Changes last until the server stops. The fixtures' times (Thu 12 Nov 2026, 09:00 in Abu Dhabi) move to the moment the server starts; set `CARELOOP_MOCK_NOW=2026-11-12T09:00:00+04:00` to keep them.

## Configuration

| Setting | Where | Default | Purpose |
|---|---|---|---|
| `CARELOOP_API_URL` | `vite`, `vite preview` | `http://localhost:5080` | Where `/api` is forwarded |
| `CARELOOP_MOCK_API` | `vite`, `vite preview` | unset | `1` answers `/api` from `mock/` instead |
| `CARELOOP_MOCK_NOW` | `vite`, `vite preview` | server start | The moment the mock's fixtures describe |

Settings can also go in `.env.local`. Times are shown in Abu Dhabi time (`TIME_ZONE` in `src/format/time.ts`) on the 24-hour clock. All text is in `src/locales/en.json`; layouts use logical CSS so Arabic can follow.

**Screen widths.** Every screen works from 640 px (a side panel) upwards without sideways scrolling, and content is at most 1200 px wide. Below 900 px the worklist shows one card per case, the case shows a compact stage bar, and filters and columns stack; from 900 px the worklist has a table per group and the case shows all ten stages; from 1200 px the worklist adds a status column and the case adds a side column. Controls are at least 44 px tall.

## Who sees what

The header's "Signed in as" menu offers six invented staff, remembered in this browser: Huda Saleh (nurse navigator), Dr. Rekha Varghese and Dr. Sara Al Hammadi (clinicians) and Joy Bautista (facility lead), all at the Family Health Centre; Dr. Layla Haddad (receiving clinician, General Hospital); and Dr. Obaid Al Suwaidi (programme coordinator, no facility). This is the API's prototype identity scheme; real sign-in replaces it before any real patient data.

- **Identity.** Every call states the person, role and facility in `X-Demo-User`, `X-Demo-Role` and `X-Demo-Facility` (`src/data/client.ts`). The coordinator sends no facility. `X-Demo-User` is the person's reference without a FHIR type (`rekha-varghese`), the form the API stores staff in.
- **Team and reasons.** Reads default to the person's facility. Opening a case at another facility first asks why, as on board 14 ("Opening a case outside your team"); the worklist's "View another team's cases" asks for the team and a reason. The reason goes as `reason` on every read of that case (case, timeline, other screening) and is remembered for this sign-in only. A refusal (403) reads calmly.
- **Coordinator.** Sees measures for all facilities, and cases only while one is escalated to them, without names, MRNs or the hospital system's own numbers: the screens say "Name hidden for your role", show a case number, and say "Result from the hospital system" without the result number. Outcome evidence references, and the wording of timeline entries from a hospital system or the patient channel, which can quote those numbers, are left out too. The rule goes by role (`namesHiddenFor` in `src/identity/roles.ts`), so it holds even if the API sends them.
- **Names.** Staff see the patient's name with the MRN as the second identifier; without a name, a friendly number such as "Patient 100 245 891".
- **Escalations.** The facility lead and the coordinator have an Escalations screen with a count. Only the role the latest open escalation went to, at the case's facility, sees "Acknowledge", and a confirmation says acknowledging does not change who is responsible or stop the clock. A 409 (already acknowledged or replaced) or 403 reads calmly and the screen reads the case again.
- **Clinicians** (clinician, receiving clinician) see a patient's other screening and the handoff summary; the API refuses the patient search to other roles.
- **People.** Owners and actors are matched to the sign-in list by `sameReference` (`src/identity/staff.ts`), which ignores a FHIR type in front, so `Practitioner/joy-bautista` and `joy-bautista` are the same person.

## Screens

| Address | Screen | Reads |
|---|---|---|
| `#/` | Worklist (1): open cases by what needs action now, with filters, search by name, MRN or number, and how many are shown | `GET /cases`, page by page (`take` 200, up to 1,000 cases) |
| `#/cases/{id}` | Case (2): where the case is (the API's display label), the next action, escalation state, tracks, timeline with its sources in words, other screening for clinicians | `GET /cases/{id}`, `/timeline`, `GET /cases?patientReference=…`, `GET /programmes/{id}` |
| `#/escalations` | Escalations: open escalations, those needing the signed-in person first | `GET /cases?escalated=true`, then each case |
| `#/cases/{id}/escalation` | Escalation (17): what is late and why, who is responsible, how late, the ladder from the programme with the current level (if the programme cannot be read, the escalations so far and a calm note), the notice history, "Acknowledge" | `GET /cases/{id}`, `/timeline`, `GET /programmes/{id}`, `POST …/escalations/{escalationId}/acknowledgement` |
| `#/cases/{id}/summary` | Handoff summary (18): draft, edit, approve exactly the edited text, or reject. An open draft and its edits are kept in memory, never in browser storage, while the reviewer looks at other screens, until they approve or reject it or someone else signs in; the API cannot send a draft back, so reloading the page loses it | `POST /cases/{id}/handoff-summary`, `…/approval`, `…/rejection` |
| `#/impact` | Impact (3): measures with what each is out of, median time to each step, closures by reason, cases by stage, patient confidence, and the comparison with and without reminders | `GET /metrics`, `POST /metrics/simulation` |
| `#/programmes`, `#/programmes/{id}` | Programmes (4): steps with deadline, reminder and owner, citations, what happens when a step is late, evidence and closure rules, change history | `GET /programmes`, `GET /programmes/{id}` |

The dashboard shows what the API sends and makes nothing up: display labels as given (decision 19), `sourceSystem` as CareLoop, the patient channel or a hospital system, a refused timeline entry as refused (never as done), durations from .NET `TimeSpan` (`1.00:00:00`) or ISO 8601, and a rate with nobody to count as "Not measured yet", never 0%. A feature the API answers with 501 (the AI summary, WS9; the comparison, WS10) shows a calm "isn't ready yet".

## Test it

```powershell
npm run lint        # oxlint (react, jsx-a11y, typescript; warnings fail), then ESLint with eslint-plugin-tsdoc and eslint-plugin-jsdoc
npm run typecheck   # tsc -b, strict, for the app and for vite.config.ts and mock/
npm test            # Vitest, in jsdom
npm run build       # lint, typecheck, then the production bundle in dist/
```

The unit tests (`src/**/*.test.ts`, run in jsdom with `src/testing/setup.ts`, builders in `src/testing/`) cover the data layer (identity headers, the reason parameter, paging, failures including 403, 409 and 501, cancellation, the exact approval payload, the acknowledgement call), coordinator redaction, names, worklist grouping and ordering, display labels, timeline sources and refused entries, who may acknowledge, the escalation ladder and notices, durations, rates and the programme and impact formatting.

The screen tests (`src/**/*.test.tsx`) open the whole dashboard with `renderApp` (`src/testing/screens.tsx`), signed in as one of the six staff, and act on it with Testing Library and user-event. A fake API (`src/testing/api.ts`) answers the app's own `fetch`, so the real client and identity headers run, and fails any test whose screen calls a route the test did not answer; `src/testing/browser.ts` fills what jsdom lacks (media queries, modal dialogs, scrolling). They cover:

- **Handoff summary.** Approving sends exactly the edited text, untrimmed, and only after the confirmation; the draft and its edits survive leaving the page and coming back (D1), and are forgotten once approved or rejected, when the API says the draft was decided or replaced, and when someone else signs in; rejecting needs a reason; a 501 reads calmly as not ready yet; the coordinator sees where the result came from, without its number.
- **Escalation.** "Acknowledge" shows only for the role the latest open escalation went to, at the case's facility, and never for another facility's lead; the confirmation says acknowledging changes neither who is responsible nor the clock; success shows the toast and the acknowledged state; a 409 reads calmly, and closing it clears the message and reads the case again (D3); a programme that cannot be read leaves the rest of the page working (D5).
- **Case.** The coordinator sees a case number and the note that names, MRNs and hospital record numbers are hidden, never a name, MRN, result number, outcome evidence reference or hospital wording that quotes one, even when the API sends them; other roles see them; only clinicians see the patient's other screening; a 403 reads calmly as not open to you; a programme that cannot be read leaves the case working (D5).
- **Worklist.** Every case is named by its patient, in tables and on cards, so no two share a name; another team's cases ask for a reason, then send `reason` and `facilityId` with every read; the coordinator searches by case number, never by name, MRN or patient reference.
- **Identity.** Every request states the person, role and facility in the `X-Demo-*` headers, with no facility for the coordinator, including straight after a change of person.

Every exported component, hook, function, type, interface and constant has TSDoc; ESLint fails the build when one is missing or malformed. End-to-end tests (Playwright) belong to WS10.

## API types

`src/api/schema.d.ts` is generated from [`contracts/openapi/careloop.json`](../../contracts/openapi/careloop.json), which the API build rewrites. Never edit it by hand; after the contract changes, run:

```powershell
npm run generate:api
```

`scripts/generate-api.mjs` runs `openapi-typescript`, a dev dependency. TypeScript stays on 5.9 because `openapi-typescript` and the ESLint TypeScript parser need its compiler API. `src/data/` wraps the generated types in an `openapi-fetch` client and small read hooks; nothing hand-written goes in `src/api/`. The identity headers are not in the OpenAPI document, so the client adds them in a middleware.

## Code

| Folder | What it holds |
|---|---|
| `src/api/` | Generated API types |
| `src/data/` | API client and identity headers, paged list reads, read hooks, acknowledgement and summary calls |
| `src/domain/` | Display rules: display stages, status words, next actions and why they are needed, patients and names, escalations (who may acknowledge, the ladder, notices), tracks, people, timeline entries |
| `src/features/` | One folder per screen |
| `src/components/`, `src/hooks/` | Header, icons, status pills, dialog, toast, states (loading placeholders, not ready yet, not open to you, error, empty), the reason form, shared hooks |
| `src/i18n/`, `src/locales/`, `src/format/`, `src/routing/`, `src/identity/` | Text, dates, durations and numbers, readable names, the hash router, sign-in and reasons for other teams |
| `src/testing/` | Test setup (DOM matchers from Testing Library, the fake API at the `fetch` level and what jsdom lacks), builders of synthetic API responses, and `renderApp` for the screen tests |
| `mock/` | The local mock API (dev and preview only) |
| `scripts/` | `generate-api.mjs`, which regenerates `src/api/schema.d.ts` |
