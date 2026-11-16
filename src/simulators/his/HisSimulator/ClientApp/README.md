# HIS simulator ClientApp

React/TypeScript/Vite source-system UI, with a visible synthetic-data banner on every screen.
The four features are scenario replay/progress, addressed staff inbox, exact FHIR request/response
logs, and explicit clinician/source actions (review, consultation, referral, appointment, outcome,
closure and caregiver consent).

All UI text uses `src/locales/en.json`; strict types, export/property TSDoc, logical CSS and hash
routing are enforced. Each page accepts an optional simulator `apiBaseUrl`, never a CareLoop REST
address. Reads poll without overlapping calls and abort on changed filters/navigation.
Opening notices never accepts ownership. Clinical code inputs have no invented defaults.

Node 24+, Yarn 4.18.1:

```powershell
corepack yarn install --immutable
corepack yarn dev
corepack yarn test
corepack yarn build
corepack yarn format:check
```

Use `npx --yes @yarnpkg/cli-dist@4.18.1` in place of `corepack yarn` when Corepack is unavailable;
do not activate global tooling. Dev port 5181 proxies to the simulator on 5081. Build includes
documentation lint and type-checking. `dist` is copied into `wwwroot` by Docker.

The compatible ESLint 9/TypeScript 5.9 toolchain and transitive build tools are pinned in the
manifest/lockfile. Yarn supplies the TSDoc plugin's missing TypeScript peer metadata in `.yarnrc.yml`.
Unit tests use React DOM/Vitest/jsdom and synthetic fetch responses, not full E2E journeys.

Cohorts are explicitly unavailable until WS10. A completed replay may include refused steps and
does not assert completed care. Shared happy-path evidence still needs WS10 correction, and patient
behaviour remains WS8. See the [host README](../../README.md) for HTTP/configuration and demo details.
