# CareLoop Python AI service

Stateless FastAPI service for the six purpose-specific draft capabilities in the [AI specification](../../docs/ai-design/ai-spec.md). Python validates typed requests, calls an optional Microsoft Foundry deployment, validates structured responses, and returns reviewable drafts. It has no database, FHIR, case-write, messaging, or workflow credentials.

The .NET API remains the source of truth for authorization, case state, ownership, deadlines, escalation, closure, exact metrics, persistence, audit, template selection, human approval, and dispatch. The AI never diagnoses, sets urgency, answers a patient, changes evidence, routes a reply, or performs an action. Every output is a draft requiring human review.

## Agents and endpoints

| Agent | `purpose` | Input boundary and safety gate |
|---|---|---|
| AG1 clinician handoff | `clinician_handoff` | Finalized source facts; every key point cites supplied fact IDs |
| AG2 closure review | `closure_review` | API-computed checklist; evidence references and statuses must be returned unchanged |
| AG3 patient message | `patient_message` | API-selected approved template; locked text and slots are checked, SMS rejects result-bearing content |
| AG4 UAE Arabic translation | `translation` | Clinician-approved English text; source references and numeric tokens must be preserved |
| AG5 reply triage | `reply_triage` | Requires deterministic precheck completion; deterministic flags are copied through and AI additions/routes are allowlisted |
| AG6 stage insights | `stage_insights` | Requires privacy-reviewed, pre-aggregated API metrics; narratives cite metric IDs and contain no numeric claims |

- `GET /health` reports process liveness.
- `GET /ai/v1/health` reports whether inference is enabled and the configured provider/deployment, without exposing the endpoint or credentials.
- `GET /workflow/stages` is an informational scaffold map only; it does not control the workflow.
- `POST /ai/v1/drafts` accepts the discriminated AG1–AG5 request union.
- `POST /ai/v1/insights` accepts AG6 aggregate metrics.

With the default provider setting (`disabled`), AI endpoints return HTTP 503; they never return a fabricated or template-only success response.

## Run and test locally

Use Python 3.13 from this directory:

```sh
python -m venv .venv
# Activate .venv for your shell, then:
python -m pip install --editable ".[dev]"
uvicorn careloop_ai_workflow.main:app --reload --port 8081
```

Run `ruff check .` and `pytest`. Tests use synthetic fixtures and a fake provider; they do not call Foundry.

## Microsoft Foundry configuration

The service uses `AIProjectClient.get_openai_client()` with the Responses API, Pydantic structured outputs, and `store=false`. Configure:

| Setting | Default | Description |
|---|---:|---|
| `CARELOOP_AI_PROVIDER` | `disabled` | Set to `foundry` only after deployment prerequisites are complete |
| `AZURE_AI_PROJECT_ENDPOINT` | empty | Microsoft Foundry project endpoint |
| `AZURE_AI_MODEL_DEPLOYMENT_NAME` | empty | Exact model deployment name |
| `CARELOOP_AZURE_HOSTED` | `false` | `false` uses `DefaultAzureCredential`; `true` uses `ManagedIdentityCredential` |
| `AZURE_CLIENT_ID` | empty | Optional user-assigned managed identity client ID |
| `CARELOOP_FOUNDRY_PROMPT_SHIELDS_ATTESTED` | `false` | Must be `true` to run Foundry, only after document-attack blocking is configured on the deployment |
| `CARELOOP_AI_MAX_INPUT_BYTES` | `262144` | Request-body limit for `/ai/v1/*` |
| `CARELOOP_AI_TIMEOUT_SECONDS` | `45` | Bounded inference timeout |
| `CARELOOP_AI_PROMPT_VERSION` | `careloop-ai-v1` | Version recorded with every output |

Before enabling inference, assign a Microsoft Foundry guardrail to the selected **model deployment** with Prompt Shields for user prompt attacks and indirect/document attacks at the user-input intervention point, using a blocking action. Verify the behavior in the Foundry guardrail playground. This repository cannot inspect or change a Foundry resource; `CARELOOP_FOUNDRY_PROMPT_SHIELDS_ATTESTED` is an operator attestation, not remote verification. The service fails closed when the attestation is absent.

Use a developer-approved Entra identity locally. In the hosted runtime, set `CARELOOP_AZURE_HOSTED=true` and use the workload's managed identity with inference-only permissions. Do not store keys, tokens, patient data, or live endpoint values in source control or container images. Request bodies and model responses are not logged or persisted by this service.

The Python endpoints do not yet validate the calling .NET API's service identity. Keep the service on a trusted private network; do not expose it to untrusted clients or enable real patient data until API-to-service authentication is implemented and verified.

## Operational and clinical limits

AG5 requires the .NET API to run the clinically approved deterministic checks first and provide `precheck_completed=true`, deterministic flags, allowed categories, additional flags, and routing options. This service does not implement or override clinical red-flag rules. A person confirms any proposed route.

Foundry integration remains disabled by default. Before any non-synthetic data is sent, the project must approve data residency, retention, privacy review, the model deployment, the data boundary, templates, and qualified clinical/bilingual review. AI outputs are not source-of-truth records; the API records draft disposition and any approval event.
