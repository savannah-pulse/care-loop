# CareLoop web

CareLoop's own dashboard: the worklist, a case, the AI handoff summary, impact and programmes (screens 1, 2, 18, 3 and 4 in [docs/ui-design](../../docs/ui-design/README.md)). React 19, TypeScript and Vite. It reads and writes through the CareLoop API only; the hospital-system and patient-channel screens belong to the simulators.

## Run it

From this folder:

```powershell
npm ci
npm run dev                                    # http://localhost:5173, /api forwarded to http://localhost:8080
$env:CARELOOP_MOCK_API='1'; npm run dev        # local data, no API needed (PowerShell)
```

In bash, use `CARELOOP_MOCK_API=1 npm run dev`. The same works for `npm run preview` after `npm run build`.

- **Against the API.** The dev server forwards `/api/*` to `CARELOOP_API_PROXY` (default `http://localhost:8080`, where Docker Compose publishes the API) without the `/api` prefix. In the container, nginx forwards `/api/` to the `api` service, and the app is on <http://localhost:4173>.
- **Without the API.** `CARELOOP_MOCK_API=1` answers `/api` from `mock/`: the eight worklist cases of board 1, Hessa's case and timeline from board 2, the numbers of board 3 and the summary draft of board 18, typed against the generated API types. It runs only inside `vite` and `vite preview`; the app does not know it exists and the build never contains it. Approvals and rejections last until the server stops. Board times (Thu 12 Nov 2026, 09:00 in Abu Dhabi) move to the moment the server starts; set `CARELOOP_MOCK_NOW=2026-11-12T09:00:00+04:00` to keep them.

## Test it

```powershell
npm run lint        # oxlint, with the react, jsx-a11y and typescript rules; warnings fail
npm run typecheck   # tsc -b, strict, for the app and for vite.config.ts and mock/
npm run build       # typecheck, then the production bundle in dist/
```

There are no unit tests yet (Vitest is the agreed tool). Every exported symbol has TSDoc.

## API types

`src/api/schema.d.ts` is generated from [`contracts/openapi/careloop.json`](../../contracts/openapi/careloop.json). Do not edit it; after the API contract changes, run:

```powershell
npm run api:generate
```

The script runs `openapi-typescript` 7.13.0 through `npx` with TypeScript 5.9.3, so the first run needs the npm registry. `openapi-typescript` needs TypeScript 5's compiler API, which TypeScript 7 (used to build this app) no longer has, so npm cannot install it here as a dev dependency. `src/data/` wraps the generated types in an `openapi-fetch` client and small read hooks; nothing hand-written goes in `src/api/`.

## Configuration

| Setting | Where | Default | Purpose |
|---|---|---|---|
| `VITE_CARELOOP_API_URL` | build time | `/api` | Base URL of the CareLoop API, if not on the same origin |
| `CARELOOP_API_PROXY` | `vite`, `vite preview` | `http://localhost:8080` | Where `/api` is forwarded |
| `CARELOOP_MOCK_API` | `vite`, `vite preview` | unset | `1` answers `/api` from `mock/` instead |
| `CARELOOP_MOCK_NOW` | `vite`, `vite preview` | server start | The moment the mock's fixtures describe |

Settings can also go in `.env.local`. Times are shown in Abu Dhabi time (`TIME_ZONE` in `src/format/time.ts`) on the 24-hour clock. All text is in `src/locales/en.json`; layouts use logical CSS so Arabic can follow.

**Screen widths.** Every screen works from 640 px (a side panel) upwards without sideways scrolling, and content is at most 1200 px wide. Below 900 px the worklist shows one card per case, the case shows a compact stage bar, and filters and columns stack; from 900 px the worklist has a table per group and the case shows all ten stages; from 1200 px the worklist adds a status column and the case adds a side column. Controls are at least 44 px tall.

**Who is signed in.** The header's "Signed in as" menu offers four invented staff (Huda Saleh, nurse navigator; Dr. Rekha Varghese, clinician; Joy Bautista, facility lead; Dr. Obaid Al Suwaidi, coordinator), remembered in this browser. Every API call states the person and role in `X-Demo-User` and `X-Demo-Role`, the API's prototype identity scheme. Real sign-in replaces both before any real patient data. Only clinician roles see the handoff summary.

## Code

| Folder | What it holds |
|---|---|
| `src/api/` | Generated API types |
| `src/data/` | API client, identity headers, read hooks and summary calls |
| `src/domain/` | Display rules: the ten stages (C9), status words, next actions and why they are needed, tracks, people, timeline entries |
| `src/features/` | One folder per screen |
| `src/components/`, `src/hooks/` | Header, icons, status pills, dialog, toast, states (loading placeholders, not ready yet, error, empty), shared hooks (time, tab title, screen width) |
| `src/i18n/`, `src/locales/`, `src/format/`, `src/routing/`, `src/identity/` | Text, dates and numbers, readable names for patients, facilities and cited facts, the hash router, sign-in |
| `mock/` | The local mock API (dev and preview only) |

## Not connected yet: contract gaps

Most API routes still answer 501; the screens then say calmly that this part isn't ready yet, and show a friendly error with "Try again" for any other failure. Where the contract is thinner than the design, the screens show only what the contract has:

1. **Patient names.** Case reads return only `patientReference` (never a name), so screens show a number taken from it, such as "Patient 100 245 891" for `Patient/p-100245891`, with the full reference in a tooltip; names and details stay in the hospital record (a name with a second identifier is proposed as C20).
2. **Stage on the worklist.** `GET /cases` has no track statuses, so the worklist shows one label per stage (Case opened, Owner assigned, In follow-up, Closure review, Closed); the case screen shows the exact display stage. Proposal: add track statuses to `CaseSummaryResponse`.
3. **Programmes.** There is no `GET /programmes` (C1), so screen 4 says programme settings are not available yet.
4. **Impact.** Metrics are thinner than the design (C5); screen 3 shows only what `MetricsResponse` and `SimulationResponse` contain.
5. **Escalation and other screening.** No escalation acknowledgement (C15) and no read of a patient's other screening (C19), so neither appears.

Also found while building:

6. **Urgency meaning.** Urgency is an opaque code; its meaning belongs to the pathway (C1). `src/domain/urgency.ts` maps the two placeholder codes until then.
7. **Staff and facility names.** Owners and timeline actors are references; the dashboard names the people of the sign-in menu and shows anyone else by role and facility, such as "Clinician, Family Health Centre" (timeline actors by role only, as entries carry no facility), with the reference in a tooltip. Facility names are in the language catalogue for the identifiers listed in `src/format/names.ts`; any other facility identifier is put into words.
8. **Summary.** There is no read of an existing summary, so it shows only after drafting in this visit; source chips put fact identifiers into words (`screening-report-2026-11-10` reads "Screening report, 10 Nov 2026"), as there is no description of a fact; and without an AI draft nobody can submit a summary written by hand.
9. **Timeline.** Entries carry no source system (C3) or track status, so each shows its reason, its kind and who did it. The source is worked out: CareLoop for its own actions, CareLoop for a summary decision made here, and the hospital system for anything else a person did.
10. **Worklist size.** One call asks for up to 500 cases; team filtering (C4) and paging are not built.
