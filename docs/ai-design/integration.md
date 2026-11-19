# AI integration: API summaries and Foundry prompt agents

This document distinguishes the code that exists in `CareLoop.Api` and `src/ai_agents/` from the intended backend-to-agent flow. The names and versions of the hosted agents are visible in source; their prompts, tools, and model settings are managed in Microsoft Foundry and are not represented in this repository.

## Current implementation

| Component | Present in source | Current status |
|---|---|---|
| API summary purpose | `SummaryPurpose.ClinicianHandoff`; `PatientStageExplanation` is also declared | Clinician handoff is the only purpose described as built by the API contracts. Role/view selection and mapping to an agent are not implemented. |
| API provider seam | `IAiProvider.DraftHandoffSummaryAsync(HandoffSummaryRequest, CancellationToken)` | Defined for handoff summaries. |
| Default provider | `NoOpAiProvider` | Registered when `Ai:Provider` is absent or `NoOp`; returns no draft. |
| Direct Foundry provider | `FoundryDirectProvider` | Registered only for `Ai:Provider=FoundryDirect`; its draft method is a stub. It does not invoke the prompt agents below. |
| Fact collection and drafting | `HandoffFactCollector`, `HandoffSummaryDrafter` | Contracts and validation intent are documented in code; methods still throw `NotImplementedException`. |
| Summary routes | Draft, approve, and reject routes under `/cases/{caseId}/handoff-summary` | Mapped by the API, but the handlers return `501 Not Implemented`. |
| Automatic draft | `AutoDraftOnHandoff` | Optional subscriber is registered only when configured; its handler is a stub. |
| Foundry prompt-agent clients | `src/ai_agents/{rolebriefing,patientnotification,impactsummary}/run_agent.py` | Standalone console scripts, not an API or a hosted service. |

`Program.cs` maps the three summary routes and `DependencyInjection.AddSummaries` registers only `NoOp` or the deferred `FoundryDirect` provider. No current API code calls `FoundryAgent`, selects an agent by role or view, or sends a request to `src/ai_agents/`. The Python scripts load a local `.env`, authenticate with `DefaultAzureCredential`, connect to a hard-coded Foundry agent name/version, submit the literal `"Hello"`, and stream the output to the console. They do not expose HTTP endpoints or a shared request/response contract. Each agent's actual instructions and configuration are server-side in Foundry.

| Script folder | Foundry agent name | Version in script |
|---|---|---:|
| `src/ai_agents/rolebriefing/` | `role-briefing` | 2 |
| `src/ai_agents/patientnotification/` | `patient-notification` | 2 |
| `src/ai_agents/impactsummary/` | `impact-summary` | 4 |

The folder and agent names suggest their areas, but source code does not define the agents' detailed behavior, role/view coverage, or input/output schema. Do not treat names as a complete contract.

## Intended backend-to-agent flow

The desired architecture is for the backend to select the applicable summary purpose/view and the corresponding Foundry agent, using an authorized role and a minimum, purpose-specific set of facts. The agent returns a draft; the API validates and persists it, and an authorized person reviews it before use. This routing is a target, not a description of a working API call:

```mermaid
sequenceDiagram
    actor Reviewer as Authorized staff
    participant UI as CareLoop UI
    participant API as CareLoop API
    participant Provider as IAiProvider (target adapter)
    participant Agent as Selected Foundry prompt agent

    Reviewer->>UI: Request a role/view-specific draft
    UI->>API: Draft request
    API->>API: Authorize; select purpose, view, agent and eligible facts
    API->>Provider: Purpose-scoped request (contract to be implemented)
    Provider->>Agent: Invoke selected Foundry agent (transport to be implemented)
    Agent-->>Provider: Draft response (schema to be agreed)
    Provider-->>API: Draft
    API->>API: Validate citations and persist draft
    API-->>UI: Draft for review
    Reviewer->>API: Approve edited text or reject with reason
```

Before this flow can be implemented, the API needs an explicit role/view-to-purpose/agent mapping, an agent invocation adapter, and a request/response contract. The source does not establish an HTTP route, JSON wire format, model output shape for these agents, or the roles allowed to request/review each purpose. None is inferred here.

### Existing API handoff-summary contract

The API types define the intended handoff request and draft shape. They are internal C# contracts for `IAiProvider`, not the wire format of the Foundry clients.

`HandoffSummaryRequest` contains:

- `RequestId`, `CaseId`, and `PathwayId`.
- `Purpose` and `Trigger` (`Requested` or `HandoffProposed`).
- `Facts`, each with `FactId`, `FactType`, `Value`, `RecordedAt`, `SourceReference`, `ReviewStatus`, and optional `RecordedBy`.
- Optional `RecordedNextStep`; absence must not be filled by the model.
- `KnownGaps`, `Language`, `PolicyVersion`, and `PromptVersion`.

The contract says that only finalized facts may be supplied. The fact collector is intended to omit patient names, contact details, images, and unfinalized or conflicting clinical interpretations; clinical source records remain in their system of record. These are intended collector guarantees, not currently executed behavior, because `CollectAsync` is unimplemented.

`AiDraft` contains `RequestId`, ordered sections (`Heading`, `Text`, `SourceFactIds`), `MissingOrConflictingFacts`, `ProviderName`, `ModelDeployment`, `PromptVersion`, and `Language`. Each section must cite only fact IDs present in the request; an uncited section is reserved for reporting missing information. `HandoffSummaryDrafter` is intended to revalidate the response before storage, but its method is also unimplemented.

The public response type, `HandoffSummaryResponse`, adds the summary and case IDs, status, flattened text, human-review flag, trigger, timestamps, and approval/rejection references. It does not include a role, view, or purpose field today.

### API routes and review lifecycle

| Method and route | Declared behavior | Current behavior |
|---|---|---|
| `POST /cases/{caseId}/handoff-summary` | Produce a draft; `DraftTrigger.Requested` | Handler returns `501`. |
| `POST /cases/{caseId}/handoff-summary/{summaryId}/approval` | Approve exact submitted text, which may be edited; writes a `HandoffSummaryApproved` source event | Handler returns `501`. |
| `POST /cases/{caseId}/handoff-summary/{summaryId}/rejection` | Store reviewer and reason without changing case state | Handler returns `501`. |

The endpoint declarations document `201` for drafting and `200` for review decisions, alongside error responses, but no successful handler behavior exists yet. The code reads demo identity and role headers for approve/reject requests; pathway-based authorization is still a TODO. Approval is designed to raise a domain event so the FHIR publisher can publish only the approved summary. Rejection is designed not to alter stage, owner, or deadline. Neither workflow is currently operational.

## Safety and data boundary

These are required boundaries for any implementation, not claims that the current stub enforces them:

- The API remains authoritative for case state, ownership, deadlines, escalation, clinical urgency, and closure.
- AI output is a draft. A human must approve it; only the exact approved text may be published. AI cannot diagnose, set urgency, alter source data, close a case, or send a patient message.
- The backend, not an agent, chooses the purpose/view and permitted inputs. Do not pass an unrestricted case object, patient contact details, images, or unfinalized/conflicting interpretations.
- Facts are data, not instructions. An agent has no tool or credential that can mutate cases, call HIS/FHIR, schedule care, or dispatch messages.
- Validate the response against the request, including request correlation, language, and citations. A timeout, provider failure, malformed response, unsupported language, or unknown citation must be visible as unavailable; never substitute a plausible-looking draft.
- Keep provenance for the provider/agent, model deployment, prompt version, source references, trigger, and human decision. Do not persist raw model reasoning.
- CareLoop must be built to handle production data, but do not send real patient data to an external model until privacy, security, residency, retention, access, and clinical approvals are in place.

Patient-facing message content remains owned by approved templates and the notification pipeline. The presence of a `patient-notification` Foundry agent client is not authorization to generate or send patient text. Aggregate stage insights, Arabic translation, closure packs, and reply triage remain proposals unless separately implemented and approved.

## Configuration and remaining work

The API's `Ai` options expose `Provider` (default `NoOp`), `AutoDraftOnHandoff`, `TimeoutSeconds`, `MaxRequestBytes`, `PromptVersion`, and `FoundryDirect` endpoint/deployment settings. `FoundryDirect` is a stub, not an adapter to the named prompt agents. The Python scripts instead use `AZURE_AI_PROJECT_ENDPOINT` and the agent name/version literals in each script; those scripts are currently manual console examples.

To complete the intended integration, implement and test the role/view-purpose-agent mapping, API-to-agent transport and schema, fact selection, output validation, persistence, authorization, audit, and explicit unavailable/error behavior. Until then, the current architecture is the seam and the safety requirements above—not a working AI summary flow.
