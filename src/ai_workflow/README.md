# CareLoop Python AI service

Python service scaffold for on-demand AI summaries and narratives described in [`docs/ai-design/ai-spec.md`](../../docs/ai-design/ai-spec.md). It currently exposes a health endpoint and a draft stage map; it does not call an LLM, store workflow state, or write cases.

**Scope boundary:** the backend coordinates the ten-stage care journey and computes all workflow state, deadlines, escalations, and metrics. Python must not duplicate those functions or propose case mutations. The current stage-oriented proposal coordinator is exploratory scaffold code only; before Foundry integration, align it with the FR-11 specification's purpose-specific summary and aggregate-narrative requests. All model outputs are drafts and require human review. Case closure, clinical interpretation, urgency, treatment decisions, consent, referral acceptance, and patient-message sending are never agent actions.

## Run locally

Use Python 3.13 and run from this directory:

```sh
python -m venv .venv
# Activate .venv for your shell, then:
python -m pip install --editable ".[dev]"
uvicorn careloop_ai_workflow.main:app --reload --port 8081
```

- `GET /health` reports service health.
- `GET /workflow/stages` exposes the current scaffold stage map; it does not control case transitions.

Run `ruff check .` and `pytest` from this directory. The FastAPI service uses a provider-neutral interface; no model provider or secret is configured.

## Implementation direction

Implement the purpose-specific summary and aggregate-narrative contracts from the AI specification; do not extend the stage-oriented proposal coordinator as a workflow engine. The API must construct minimum-necessary requests from approved, finalized facts or privacy-reviewed aggregates. Keep source references with every factual output and reject malformed or unsupported model responses.

Before connecting Microsoft Foundry, the team must approve the deployment, data boundary, reviewed templates, and required clinical and bilingual reviewers. Keep credentials outside source control. All generated content remains a human-reviewed draft, with the .NET API enforcing authorization and recording disposition.
