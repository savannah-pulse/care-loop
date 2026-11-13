## What this changes

<!-- One or two sentences. Name the workstream (WS1 to WS10). -->

## Checklist

- [ ] **Builds:** `dotnet build` passes with zero warnings for every solution I touched; `yarn lint`, `yarn typecheck` and `yarn build` pass for every front end I touched.
- [ ] **Tests:** relevant tests run and pass. I un-skipped the tests for the behaviour I implemented and did not weaken or delete any test.
- [ ] **Docs:** every public and internal C# member has XML docs; every exported TypeScript symbol has TSDoc; READMEs and `docs/workstreams.md` are updated if files moved or were added.
- [ ] **Nothing real in the repository:** no real patient data, credentials or live endpoint addresses are committed; the API and dashboard carry no simulated-data flags or labels (that is the simulators' job).
- [ ] **Constraints:** no clinical decision, no hard-coded clinical deadline, no closure without an authorized human event, AI output remains a draft.
- [ ] **Scope:** I only edited files my workstream owns (see `docs/workstreams.md`), or I agreed the change with the owning workstream.
- [ ] **Dependencies:** no new package beyond those listed in `AGENTS.md`, or it was agreed first.
- [ ] I removed the `TODO(WS-n)` tags for the stubs I implemented.

## How I tested it

<!-- Commands run and their result. Say plainly what was not tested. -->
