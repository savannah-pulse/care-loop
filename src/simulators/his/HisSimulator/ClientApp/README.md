# HIS simulator ClientApp

React, TypeScript and Vite UI for the HIS simulator. **Simulated data only**; a banner says so on
every screen. Status: placeholder screens (WS7).

| Feature folder                   | Screen                                                         |
| -------------------------------- | -------------------------------------------------------------- |
| `src/features/scenario-runner`   | Scenario runner                                                |
| `src/features/staff-inbox`       | Staff inbox                                                    |
| `src/features/fhir-log`          | FHIR message log (sent and received)                           |
| `src/features/clinician-actions` | Act as clinician (review, referral decision, outcome, closure) |

Rules: every export has a TSDoc comment (enforced by ESLint); all user-visible text comes from
`src/locales/en.json` through i18next; CSS uses logical properties only; routing is hash-based so
client routes never collide with the simulator's API paths.

```sh
corepack yarn install
corepack yarn dev          # http://localhost:5181, proxies API calls to http://localhost:5081
corepack yarn lint && corepack yarn typecheck && corepack yarn test && corepack yarn build
```
