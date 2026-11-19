# AI integration: three short texts written by a model

How the API uses AI: what it is for, what is sent, where the instructions live, how to change provider, and how it fails.

This document is the contract. [`ai-spec.md`](ai-spec.md) is the earlier design proposal and is superseded here where the two differ.

> **What changed (19 November 2026).** The API calls the model itself. The Python AI workflow service is retired, so decision 8 ("the Python service calls Foundry and the API calls the Python service") is reversed. The AI handoff summary (FR-11) is removed: its endpoints, contracts, table and screen are gone. The design is deliberately small: build the data, put it into a prompt file, send it, return the text.

## What AI is used for

| Use | Where staff see it | Endpoint | Prompt file |
|---|---|---|---|
| Role briefing | Home page: a short summary of what the signed-in role has in front of them | `GET /ai/role-briefing` | `role-briefing.md` |
| Impact summary | Impact page: a short reading of the figures shown | `GET /ai/impact-summary` | `impact-summary.md` |
| Patient message draft | Case page: a draft of a message to the patient, for a nurse navigator or clinician to read | `GET /cases/{caseId}/patient-message-draft` | `patient-message.md` |

Each returns `AiTextResponse`: `text`, `model`, `generatedAt` and `cached`.

## The boundary

AI makes already-recorded information easier to read. It does not operate the workflow.

- AI output is a draft, shown to staff labelled as written by AI. It is not stored and nothing acts on it.
- AI cannot set urgency, alter source data, change a case, close a case or send a message. It is given no tool and no way to.
- AI never discloses a result. The reporting physician informs the patient (DoH §3.8.1), so the patient message prompt carries nothing that could reveal one.
- Messages that reach a patient still come from the approved templates under `Infrastructure/Templates`. The draft is a preview for staff.
- Failure is visible. There is no stand-in text: "AI is off" (409), "AI wrote this" (200) and "AI could not write this" (503) are different answers.

## How it works

```
endpoint ──▶ build the data ──▶ put it into the prompt file ──▶ IAiClient ──▶ model
   │                                                                │
   └── access checks and read audit, as for any read                └── chosen by Ai:Provider
```

1. The endpoint checks who is asking. While `Ai:Provider` is `NoOp` it answers 409 before anything is read.
2. It reads what the reader may see, with the same access rules as the screen, and records the read in the audit.
3. It builds a small data object from the fields it is allowed to send.
4. `AiText` writes the data as JSON into the prompt file where the file says `{{data}}`.
5. `IAiClient` sends that prompt to the model and returns its text.

All of it is in `src/api/CareLoop.Api/Features/Ai` (the three endpoints, `AiText`, `IAiClient`) and `Infrastructure/Ai` (settings, the prompt loader, the provider clients).

## The prompt files

`src/api/CareLoop.Api/Prompts/` holds one plain-text file per use. A file is the instructions for the summary, with `{{data}}` where the data goes:

```
You are writing the short summary shown above the figures on the Impact page ...

Rules:
- 2 to 4 sentences of plain text, at most 120 words.
- Every number must appear in the data exactly as written.
...

Data:
{{data}}
```

- **To change what the model is asked,** edit the file and restart the API. No code changes.
- What is in the file is what is sent, with `{{data}}` replaced by the data as JSON.
- A file that is missing, or does not contain `{{data}}` exactly once, stops the API at start-up with a message naming the file.
- `Ai:PromptsDirectory` can point at another folder for one environment.

The files decide how the data is summarised. Which data is sent is decided in code, so editing a prompt cannot send something that should not leave CareLoop. [`Prompts/README.md`](../../src/api/CareLoop.Api/Prompts/README.md) is the editing guide.

## What is sent

### Role briefing

The open cases in the reader's own scope, the same ones `GET /cases` gives them.

| Field | Meaning |
|---|---|
| `reader_role`, `today` | Who is reading, and the date |
| `open_cases`, `overdue`, `due_today`, `escalated`, `without_owner` | Counts over every case in scope |
| `cases[]` | Up to 25 of the most pressing cases: `step`, `next_action`, `deadline`, `status` (`overdue`, `due today`, `upcoming`), `escalated`, `needs_owner`, `held_by` (a role), `yours`, `patient_message` |

**Never sent:** patient names, record numbers, references, contact details, case identifiers or numbers, staff names, free text.

### Impact summary

The same numbers the impact page shows, read through `GET /metrics`'s own handler, so the access rules, the audit entry and the figures are identical.

| Field | Meaning |
|---|---|
| `facility` | The facility the figures cover, or "every facility" |
| `total_cases`, `open_cases`, `closed_cases` | Totals |
| `open_cases_past_deadline`, `open_cases_escalated`, `open_cases_without_owner` | Shares of the open cases, as "8 of 20 (40%)" |
| `rates` | Each rate the page shows, the same way |
| `median_time_from_result_to` | The median time to each step, with how many cases it rests on |
| `open_cases_waiting_because`, `closed_cases_by_reason` | Counts by reason |

**Never sent:** anything about a single case. **Small numbers:** a count from 1 to 4, and a share that rests on fewer than 5 cases or has fewer than 5 on either side, is sent as "withheld: fewer than 5". The totals are withheld together when one of them is small, and the closure reasons are withheld together when one of them is small, so a withheld number cannot be worked out by subtraction. The screen still shows staff every exact number. The threshold (`ImpactSummary.SmallCount`) is a placeholder pending a governance decision.

### Patient message draft

For a nurse navigator or a clinician, for one case they may read.

| Field | Meaning |
|---|---|
| `channel`, `max_characters` | "text message" (459) or "email" (1000) |
| `clinic` | The facility's name |
| `purpose` | What the message is for, in a sentence with no result in it |
| `appointment` | An upcoming appointment's day and time, or "none" |

**Never sent:** the patient's name (the draft says `{first name}`), contact details, identifiers, the result, the appointment's place, or the case's next action.

**The purpose.** Several next actions reveal clinical state ("review the result", "start the treatment"). Telling a model not to hint at a result cannot help once the input is the hint, so the API maps the next action to a purpose and the next action itself never reaches the prompt. The mapping is in code (`PatientMessageDraft.PurposeFor`), not in the prompt file:

| Next action | Purpose |
|---|---|
| `acknowledge-notification` | The care team sent the patient a message and would like to know that it arrived |
| `call-patient` | The care team will call the patient back, as the patient asked |
| `contact-patient-another-way` | The care team asks the patient to get in touch |
| `rebook-appointment` | The care team would like to find a new time for the patient's appointment |
| Every other next action | The care team would like to talk with the patient |

## The answer

The model's text is returned as it was written, trimmed. The API does not parse it or check it against the data. That is a deliberate simplification: the texts are short drafts that staff read next to the real figures and worklist, and nothing is sent or stored. The instructions in the prompt files are what keeps the text to the data.

## Reusing an answer

The role briefing and the impact summary are kept in the API's memory for `Ai:CacheHours` (6 by default), under the model and the exact prompt. Opening the page again with unchanged data does not call the model again; any change in the data, or in the prompt file, is a different prompt and asks again.

- `?refresh=true` asks again and replaces the kept answer.
- `cached` is `true` for a reused answer, and `generatedAt` is when it was first written.
- Access checks and the read audit run on every request, reused answer or not.
- The memory is lost on restart and is not shared between instances, which costs one more call. A failure is never kept. `Ai:CacheHours` of `0` switches reuse off.

The patient message draft is never reused.

## Changing the AI provider

The endpoints know only this:

```csharp
public interface IAiClient
{
    string Model { get; }
    bool IsEnabled { get; }
    Task<string> CompleteAsync(string prompt, CancellationToken ct);
}
```

| `Ai:Provider` | Client | What it does |
|---|---|---|
| `NoOp` (default) | `NoOpAiClient` | AI is off. Every AI endpoint answers 409 |
| `Foundry` | `FoundryAiClient` | Calls a model deployment in a Microsoft Foundry project |

To use another provider, for example the Anthropic API:

1. Write one class that implements `IAiClient`: send the prompt, return the text.
2. Add a case for it in `DependencyInjection.AddAi`, with whatever settings it needs (an API key belongs in the environment's secret store, never in the repository).
3. Set `Ai:Provider`.

The endpoints, the prompt files, the data and the tests do not change, because the whole prompt, instructions included, is already in the files.

### The Foundry client

`FoundryAiClient` sends `POST {Ai:Foundry:ProjectEndpoint}/openai/v1/responses` with `{ "model": "<Ai:Foundry:Model>", "input": "<the prompt>" }` and an Entra bearer token (scope `https://ai.azure.com/.default`). There is no key. The token comes from `DefaultAzureCredential`: the API's managed identity in Azure (`apps` in `infra/azure`), the developer's own `az login` locally. That identity needs the **Foundry User** role on the Foundry account.

The project's hosted agents (`role-briefing`, `impact-summary`, `patient-notification`) and the samples in `src/ai_agents/` are not used: their instructions live in the Foundry portal, which made the wording hard to change and the provider hard to swap.

## Failure

| Situation | Answer |
|---|---|
| `Ai:Provider` is `NoOp` | 409 "AI is switched off"; nothing was read or sent |
| The model cannot be reached, answers an error or 429, writes nothing, or does not answer within `Ai:TimeoutSeconds` | 503 "AI could not write this" |

The reason is logged; the prompt and the text never are. Nothing else on the page depends on the text.

## Configuration

Section `Ai` (`Infrastructure/Ai/AiOptions.cs`):

| Setting | Default | Purpose |
|---|---|---|
| `Ai:Provider` | `NoOp` | `NoOp` (AI off) or `Foundry`. Any other value stops the host |
| `Ai:TimeoutSeconds` | `30` | How long to wait for an answer |
| `Ai:CacheHours` | `6` | How long an answer is reused; `0` switches reuse off |
| `Ai:PromptsDirectory` | `Prompts` | Where the prompt files are read from |
| `Ai:Foundry:ProjectEndpoint` | empty | `https://{resource}.services.ai.azure.com/api/projects/{project}`; `https` only. Never committed |
| `Ai:Foundry:Model` | `gpt-6.1-sol` | The model deployment that writes the texts |

- **Docker Compose** passes `AI_PROVIDER`, `AZURE_AI_PROJECT_ENDPOINT`, `AZURE_AI_MODEL_DEPLOYMENT_NAME`, `AI_TIMEOUT_SECONDS` and `AI_CACHE_HOURS`; see `.env.example`. A container has no `az login`, so a live check uses `dotnet run`.
- **Azure** (`infra/azure`): `API_AI_PROVIDER` sets `Ai__Provider`; `AZURE_AI_PROJECT_ENDPOINT` and `AZURE_AI_MODEL_DEPLOYMENT_NAME` set the endpoint and model; with `AZURE_AI_FOUNDRY_ACCOUNT_ID` the deployment gives the API's identity Foundry User on that account.

### Trying it against a live model

Run the API (it needs its database):

```bash
az login
Ai__Provider=Foundry Ai__Foundry__ProjectEndpoint=https://<account>.services.ai.azure.com/api/projects/<project> dotnet run --project src/api/CareLoop.Api
```

Or, without a database, run the opt-in test. It runs the three real endpoints with synthetic cases held in memory and does nothing unless the endpoint is set:

```bash
CARELOOP_LIVE_FOUNDRY_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project> dotnet test tests/api/CareLoop.Tests --filter "FullyQualifiedName~Live_"
```

Run on 19 November 2026 against `gpt-6.1-sol`: all three endpoints answered 200, the repeated briefing was reused, and the patient drafts kept `{first name}` and the reply instructions.

## What is built

| Piece | State |
|---|---|
| `IAiClient`, `FoundryAiClient`, `NoOpAiClient` | Built |
| The three endpoints, `AiText` (prompt, reuse, 409 and 503) | Built |
| Prompt files and `AiPrompts` | Built |
| Migration `RemoveHandoffSummaries` (drops the `HandoffSummaries` table) | Built |
| Tests | `tests/api/CareLoop.Tests/Api/Ai/` |
| Dashboard display of the three texts | Not built yet (WS6). The handoff summary screen is removed from `src/web` |

## Before real data

Synthetic only today. Before any non-synthetic data reaches a model, the organization must confirm the region and data residency (all patient data and AI processing stay in the UAE), retention and logging settings, ADHICS controls, access roles, the network path, Prompt Shields on the model deployment, and the applicable DoH and legal approvals. The same applies before pointing `Ai:Provider` at any other provider. `impact-summary.md` ends its summary with "Based on synthetic demonstration data." until then.

## Open

- Whether staff-only AI text may be shown with a label and no approval step. It is today; record the decision.
- Sending an AI-drafted message to a patient. Today the draft is a preview and patients receive approved templates. Sending one would need a person's approval of the exact text, recorded as an event.
- Which roles may ask for a patient message draft. Today a nurse navigator or a clinician.
- The small-number threshold.
- The model's text is not checked against the data. If a wrong summary is ever seen, add a check for that case rather than a general parser.
- `src/ai_agents/` and the hosted agents in the Foundry project are no longer used by the API; decide whether to keep them.
