# CareLoop Python AI workflow

Python service scaffold for coordinating the patient journey across the ten stages documented in [`docs/architecture.md`](../../docs/architecture.md). It defines the stage map, role-specific agent interfaces, synthetic-only context and human-reviewable proposal contracts. It does not yet execute agent decisions, call an LLM, store workflow state, or write cases.

The Python workflow coordinates; the .NET API remains the authoritative case-state and authorization gate. Every proposed action must be reviewed and accepted by the API before it can affect a case. Case closure, clinical interpretation, urgency, treatment decisions, consent, provider acceptance, and patient-message sending are never agent actions.

## Run locally

Use Python 3.13 and run from this directory:

```sh
python -m venv .venv
# Activate .venv for your shell, then:
python -m pip install --editable ".[dev]"
uvicorn careloop_ai_workflow.main:app --reload --port 8081
```

- `GET /health` reports service health.
- `GET /workflow/stages` exposes the configured stage/agent responsibility map.

Run `ruff check .` and `pytest` from this directory. The FastAPI service uses a provider-neutral interface; no model provider or secret is configured.

## Adding an agent

Implement the `CoordinationAgent` protocol under `careloop_ai_workflow/agents/`. Agents receive only a case reference, event reference, workflow stage, and an explicit synthetic-data marker. They return a typed `AgentProposal`; there is no proposal type for diagnosis, urgency selection, consent, clinical action, direct messaging, or closure. Keep raw clinical content out of prompts and agent context.

Before connecting a model provider, the team must approve its use, the data boundary, and reviewed templates. Keep provider credentials outside source control, and retain human review and .NET authorization for every proposal.
