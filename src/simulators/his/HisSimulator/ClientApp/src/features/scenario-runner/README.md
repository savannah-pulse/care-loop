# scenario-runner

Scenario runner: lists the shared scenario files and replays the HIS steps of one against CareLoop's FHIR boundary, showing per-step progress.

`ScenarioRunnerPage` reads `/scenarios`, starts explicit runs and polls their progress with cancellation.
It distinguishes refused steps from fatal failures and disables cohort starts. Completion is replay
completion, not proof of completed care. Shared fixture corrections and cohort expansion remain WS10.
All data shown by this feature is simulated.
