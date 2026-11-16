# CareLoop web

The staff coordination dashboard: React, strict TypeScript and Vite. It reads CareLoop's REST API, never a simulator
store, and does not make clinical decisions. Screen designs are in [docs/ui-design](../../docs/ui-design/README.md).

## Run and configuration

From this directory:

```sh
npm ci
npm run dev
```

Vite proxies `/api` to `http://localhost:5080`. Set `CARELOOP_API_URL` in the development server's environment to
use another API. The Docker image serves the built dashboard through nginx and proxies `/api/` to `http://api:8080/`.
Start the whole system with `docker compose up --build` at the repository root.

Enter a staff reference, role and facility in **Staff identity**. These become `X-Demo-User`, `X-Demo-Role` and
`X-Demo-Facility` headers, not authentication. Staff reads default to their facility; cross-facility reads require
an access reason. Coordinators see global measures and only cases with an open coordinator escalation, without
names or MRNs. Identity changes clear mounted case/draft state and cancel old reads. Nothing is stored in browser
storage. This prototype is not fit for real patient data; production requires real sign-in and authorization.

## Screens and boundaries

- Worklist: paged cases, full scoped exception queues, current-page search and recorded cross-facility access.
- Case: API-provided display label, accountable owner, next action/deadline, parallel tracks and received-order audit
  timeline with source attribution. Clinician roles can read the patient's other cases in the selected facility.
- Escalations: configured ladder, notice history and explicit confirmation before acknowledging the current notice.
  Acknowledgement never transfers ownership, clears flags or stops the clock.
- Impact: rates with numerators and denominators, elapsed medians, historically observed labels and confidence
  participation. Empty populations remain unmeasured. Simulation comparisons and AI insight narratives are deferred.
- Programmes: current pathway definitions, deadlines, clinical approval, closure/evidence rules and change history.
- Handoff summary: clinician-facing draft, source citations, missing facts, edited-text approval and rejection.
  WS9 owns the backend/provider. Its current 501/unavailable replies are displayed as failures, never invented drafts.
  Draft state is session-local; the dashboard does not imply durable latest-draft retrieval.

All UI copy uses the English catalogue; CSS uses logical properties. Source-authored identifiers and descriptions
are displayed unchanged. Local and test data must be synthetic, but the dashboard has no simulated-data branches.

## Generate contracts and validate

Build the API to refresh [careloop.json](../../contracts/openapi/careloop.json), then:

```sh
npm run generate:api
npm run test
npm run build
```

Never edit `src/api/schema.d.ts` manually. `src/api/client.ts` is the handwritten identity/error-handling wrapper.
TypeScript 5.9.x is retained for compatibility with OpenAPI generation and the ESLint parser. `npm run build` runs
oxlint, required-export JSDoc/TSDoc linting, type-checking and the production Vite build. `npm run test` runs Vitest in
jsdom with Testing Library against a mocked API client and synthetic data. It covers:

- the REST boundary (request headers, identity header validation, error mapping, non-JSON replies, exact approval payloads);
- `useResource` (abort on key change, stale-while-revalidate within a scope, no data shown across scopes);
- the worklist (paging at exactly one page, queue chips, coordinator search, rows without a deadline);
- the case screen (acknowledgement for the addressed role only, no names for a coordinator, programme-read failure
  degrading only the parts that need it, error cleared on Refresh, handoff draft kept across tabs and Refresh, approval
  sending the textarea text exactly and gated by the confirmation);
- the identity form, sorting and measure/locale formatting.

Not covered by automated tests: `Impact`, `Programmes` rendering, and any real API, browser or end-to-end journey
(Playwright journeys belong to WS10).

Validation for WS6 was done in an integrated browser against synthetic API interception, covering case/escalation
navigation and acknowledgement, role redaction, a 375px viewport, programmes/impact, summary unavailability and
exact-text approval. This is not a real API/database end-to-end journey; those journeys belong to WS10. CI runs lint,
type-checking, the Vitest suite and the production build for this folder.

`npm audit --omit=dev` reports zero findings. The installed documentation/parser tooling still reports eight moderate
development-only findings through `minimatch`; the suggested forced downgrade conflicts with TypeScript compatibility.
No forced fix or unsupported dependency override is applied.
