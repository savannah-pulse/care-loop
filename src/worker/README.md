# Deadline and escalation worker

The deadline and escalation worker (workstream WS5) runs as a `BackgroundService` inside the API: see `src/api/CareLoop.Api/Worker/DeadlineWorker.cs`, switched on with `DeadlineWorker:Enabled`. This folder holds no code. It performs deterministic checks against clinician-approved pathway configuration. A missing clock or recipient must become visible review work, not an invented deadline or urgency.
