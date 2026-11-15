# CareLoop web

CareLoop's own dashboard for staff: the worklist, a case, the AI handoff summary, impact and programmes (screens 1, 2, 18, 3 and 4 in [docs/ui-design](../../docs/ui-design/README.md)). React 19, strict TypeScript and Vite. It reads and writes through the CareLoop REST API only, never a simulator's store, and makes no clinical decisions; the hospital-system and patient-channel screens belong to the simulators.

## Run it

From this folder:

```powershell
npm ci
npm run dev                                                       # http://localhost:5173, /api forwarded to http://localhost:5080
$env:CARELOOP_API_URL='http://localhost:8080'; npm run dev        # the API that Docker Compose publishes
$env:CARELOOP_MOCK_API='1'; npm run dev                           # local data, no API needed
```

In bash, use `CARELOOP_API_URL=http://localhost:8080 npm run dev` or `CARELOOP_MOCK_API=1 npm run dev`. The same works for `npm run preview` after `npm run build`.

- **Against the API.** The dev server forwards `/api/*`, without the `/api` prefix, to `CARELOOP_API_URL`: `http://localhost:5080` by default, where `dotnet run --project src/api/CareLoop.Api` starts the API. Docker Compose publishes the API on `http://localhost:8080`. In the container, nginx forwards `/api/` to the `api` service, and the dashboard is on <http://localhost:4173>; start everything with `docker compose up --build` at the repository root.
- **Without the API.** `CARELOOP_MOCK_API=1` answers `/api` from `mock/`, typed against the generated API types. It runs only inside `vite` and `vite preview`; the app does not know it exists and the build never contains it. Changes last until the server stops. The fixtures' times (Thu 12 Nov 2026, 09:00 in Abu Dhabi) move to the moment the server starts; set `CARELOOP_MOCK_NOW=2026-11-12T09:00:00+04:00` to keep them.

## Test it

```powershell
npm run lint        # oxlint (react, jsx-a11y, typescript; warnings fail), then ESLint with eslint-plugin-tsdoc and eslint-plugin-jsdoc
npm run typecheck   # tsc -b, strict, for the app and for vite.config.ts and mock/
npm test            # Vitest unit tests
npm run build       # lint, typecheck, then the production bundle in dist/
```

Every exported component, hook, function, type, interface and constant has TSDoc; ESLint fails the build when one is missing or malformed. End-to-end tests (Playwright) belong to WS10.

## API types

`src/api/schema.d.ts` is generated from [`contracts/openapi/careloop.json`](../../contracts/openapi/careloop.json), which the API build rewrites. Never edit it by hand; after the contract changes, run:

```powershell
npm run generate:api
```

`scripts/generate-api.mjs` runs `openapi-typescript`, a dev dependency. TypeScript stays on 5.9 because `openapi-typescript` and the ESLint TypeScript parser need its compiler API. `src/data/` wraps the generated types in an `openapi-fetch` client and small read hooks; nothing hand-written goes in `src/api/`.

## Configuration

| Setting | Where | Default | Purpose |
|---|---|---|---|
| `CARELOOP_API_URL` | `vite`, `vite preview` | `http://localhost:5080` | Where `/api` is forwarded |
| `CARELOOP_MOCK_API` | `vite`, `vite preview` | unset | `1` answers `/api` from `mock/` instead |
| `CARELOOP_MOCK_NOW` | `vite`, `vite preview` | server start | The moment the mock's fixtures describe |

Settings can also go in `.env.local`. Times are shown in Abu Dhabi time (`TIME_ZONE` in `src/format/time.ts`) on the 24-hour clock. All text is in `src/locales/en.json`; layouts use logical CSS so Arabic can follow.

**Screen widths.** Every screen works from 640 px (a side panel) upwards without sideways scrolling, and content is at most 1200 px wide. Below 900 px the worklist shows one card per case, the case shows a compact stage bar, and filters and columns stack; from 900 px the worklist has a table per group and the case shows all ten stages; from 1200 px the worklist adds a status column and the case adds a side column. Controls are at least 44 px tall.

**Who is signed in.** The header's "Signed in as" menu offers invented staff, remembered in this browser. Every API call states the person and role in `X-Demo-User` and `X-Demo-Role`, the API's prototype identity scheme. Real sign-in replaces both before any real patient data. Only clinician roles see the handoff summary.

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
| `scripts/` | `generate-api.mjs`, which regenerates `src/api/schema.d.ts` |
