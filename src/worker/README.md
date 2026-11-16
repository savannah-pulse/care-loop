# Deadline and escalation worker

The deadline and escalation worker (workstream WS5) runs as a `BackgroundService` inside the API: see
[`DeadlineWorker.cs`](../api/CareLoop.Api/Worker/DeadlineWorker.cs). It is enabled through `DeadlineWorker:Enabled`,
with a technical polling interval in `DeadlineWorker:Interval`. This folder holds no code.

Every duration comes from the selected pathway definition (still a placeholder until clinically approved).
Warnings, overdue flags and escalation steps are audited and queued through the single case write path; the
outbox delivers the notices. Acknowledging an escalation never stops the clock or transfers ownership.
Failures are logged and retried, never replaced with an invented deadline or urgency.

See the [API README](../api/README.md#deadlines-and-escalation-ws5) for behaviour and test commands.
