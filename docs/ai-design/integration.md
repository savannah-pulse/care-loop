# AI integration: three short texts written by a model

How the API uses AI: what it is for, what is sent, where the instructions live, how to change provider, and how it fails.

This document is the contract. [`ai-spec.md`](ai-spec.md) is the earlier design proposal and is superseded here where the two differ.

> **What changed (19 November 2026).** The API asks the Microsoft Foundry agents itself. The Python AI workflow service is retired, so decision 8 ("the Python service calls Foundry and the API calls the Python service") is reversed. The AI handoff summary (FR-11) is removed: its endpoints, contracts, table and screen are gone. The design is deliberately small: build the data, put it into a prompt file, hand it to the provider, return the text. On Foundry each use goes to its own agent.

## What AI is used for

| Use | Where staff see it | Endpoint | Prompt file | Foundry agent |
|---|---|---|---|---|
| Role briefing | Home page: a short summary of what the signed-in role has in front of them | `GET /ai/role-briefing` | `role-briefing.md` | `role-briefing` |
| Impact summary | Impact page: a short reading of the figures shown | `GET /ai/impact-summary` | `impact-summary.md` | `impact-summary` |
| Patient message draft | Case page: a draft of a message to the patient, for a nurse navigator or clinician to read | `GET /cases/{caseId}/patient-message-draft` | `patient-message.md` | `patient-notification` |

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
endpoint ─▶ build the data ─▶ AiText ─▶ IAiClient.CompleteAsync(request)
   │                                        │
   │                 ┌──────────────────────┴───────────────────────┐
   │        FoundryAiClient                                any other provider
   │          FoundryAgentFormat: request → agent input      sends request.Prompt
   │          asks the use's agent by name                   (the prompt file with the data)
   │          FoundryAgentFormat: agent answer → text
   └── access checks and read audit, as for any read
```

1. The endpoint checks who is asking. While `Ai:Provider` is `NoOp` it answers 409 before anything is read.
2. It reads what the reader may see, with the same access rules as the screen, and records the read in the audit.
3. It builds a small data object from the fields it is allowed to send.
4. `AiText` makes one request from it: the use, the prompt file's instructions (`Task`), the data as JSON (`Data`), and the two together (`Prompt`).
5. `IAiClient` gets the text from the provider and `AiText` returns it.

All of it is in `src/api/CareLoop.Api/Features/Ai` (the three endpoints, `AiText`, `IAiClient`) and `Infrastructure/Ai` (settings, the prompt loader, the Foundry client and its translation file).

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
- For a provider that takes a prompt, what is in the file is what is sent, with `{{data}}` replaced by the data as JSON.
- On Foundry, how much of the file applies depends on the agent: `role-briefing` follows the file's instructions as its task, while `impact-summary` follows its own instructions in the Foundry project and uses only the data. See [The Foundry agents](#the-foundry-agents).
- A file that is missing, or does not contain `{{data}}` exactly once, stops the API at start-up with a message naming the file.
- `Ai:PromptsDirectory` can point at another folder for one environment.

The files decide how the data is summarised. Which data is sent is decided in code, so editing a prompt cannot send something that should not leave CareLoop. [`Prompts/README.md`](../../src/api/CareLoop.Api/Prompts/README.md) is the editing guide.

## What is sent

### Role briefing

The open cases in the reader's own scope, the same ones `GET /cases` gives them.

| Field | Meaning |
|---|---|
| `opening` | The briefing's first sentence, written by the API so its counts are always right; the model continues from it |
| `reader_role`, `today` | Who is reading, and the date |
| `open_cases`, `overdue`, `due_today`, `escalated`, `without_owner` | Counts over every case in scope |
| `cases[]` | Up to 25 of the most pressing cases: `token` (`C-1`, `C-2`, …), `step`, `next_action`, `deadline`, `status` (`overdue`, `due today`, `upcoming`), `escalated`, `needs_owner`, `held_by` (a role), `yours`, `patient_message` |

**Never sent:** patient names, record numbers, references, contact details, case identifiers or numbers, staff names, free text.

**Case numbers.** The model knows a case only by its token. When the text comes back the API replaces each token with the case number the dashboard shows, on every request, after any reuse. So a case number never leaves CareLoop, and a reused answer can only ever show the reader's own cases.

### Impact summary

The same numbers the impact page shows, read through `GET /metrics`'s own handler, so the access rules, the audit entry and the figures are identical.

| Field | Meaning |
|---|---|
| `facility`, `period` | The facility the figures cover, or "every facility", and the period in words |
| `total_cases`, `open_cases`, `closed_cases` | Totals |
| `shares` | Open cases past their deadline, escalated and without an owner, then each rate the page shows; each as `numerator`, `denominator`, `percent` |
| `median_time_from_result_to` | The median time to each step (`time`), with how many cases it rests on (`cases`) |
| `open_cases_waiting_because`, `closed_cases_by_reason` | Counts by reason |

**Never sent:** anything about a single case. **Small numbers:** a count from 1 to 4, and a share that rests on fewer than 5 cases or has fewer than 5 on either side, is sent as the word "withheld", with no number. The totals are withheld together when one of them is small, and the closure reasons are withheld together when one of them is small, so a withheld number cannot be worked out by subtraction. The screen still shows staff every exact number. The threshold (`ImpactSummary.SmallCount`) is a placeholder pending a governance decision.

### Patient message draft

For a nurse navigator or a clinician, for one case they may read.

| Field | Meaning |
|---|---|
| `channel`, `max_characters` | "text message" (459) or "email" (1000) |
| `clinic` | The facility's name |
| `situation` | Which kind of administrative message fits: `appointment reminder`, `could not reach the patient` or `please contact the care team` |
| `purpose` | What the message is for, in a sentence with no result in it |
| `appointment_date`, `appointment_time` | An upcoming appointment's day and time, or "none" |

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

The API does not parse the text or check it against the data. That is a deliberate simplification: the texts are short drafts that staff read next to the real figures and worklist, and nothing is sent or stored. What it does do:

- the role briefing gets the API's opening sentence in front, and case numbers in place of tokens;
- an agent's refusal (`CANNOT_COMPLY`, or `"status": "cannot_comply"`) or an answer in a shape the agent's instructions do not give is a failure, never shown as text.

## Reusing an answer

The role briefing and the impact summary are kept in the API's memory for `Ai:CacheHours` (6 by default), under the provider and the exact prompt. Opening the page again with unchanged data does not call the model again; any change in the data, or in the prompt file, is a different prompt and asks again.

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
    bool IsEnabled { get; }
    string Describe(string use);
    Task<string> CompleteAsync(AiRequest request, CancellationToken ct);
}

public sealed record AiRequest(string Use, string Task, string Data, string Prompt);
```

| `Ai:Provider` | Client | What it does |
|---|---|---|
| `NoOp` (default) | `NoOpAiClient` | AI is off. Every AI endpoint answers 409 |
| `Foundry` | `FoundryAiClient` | Asks each use's agent in a Microsoft Foundry project |

To use another provider, for example the Anthropic API:

1. Write one class that implements `IAiClient`: send `request.Prompt`, return the text.
2. Add a case for it in `DependencyInjection.AddAi`, with whatever settings it needs (an API key belongs in the environment's secret store, never in the repository).
3. Set `Ai:Provider`.

The endpoints, the prompt files, the data and their tests do not change, because the whole prompt, instructions included, is already in the files. Nothing about Foundry reaches them: its agent names and formats are inside the Foundry client.

### The Foundry agents

`FoundryAiClient` is the one Foundry client. For each use it asks that use's agent: `POST {Ai:Foundry:ProjectEndpoint}/openai/v1/responses` with `{ "agent_reference": { "type": "agent_reference", "name": "<agent>" }, "input": [ … ] }` and an Entra bearer token (scope `https://ai.azure.com/.default`). There is no key. The token comes from `DefaultAzureCredential`: the API's managed identity in Azure (`apps` in `infra/azure`), the developer's own `az login` locally. That identity needs the **Foundry User** role on the Foundry account.

- **Named, not versioned.** `Ai:Foundry:Agents` holds the agent names only, so the agent's latest version answers.
- **Owned elsewhere.** An agent's instructions, model and tools are set in the Foundry project by its owner. The API only calls the agents; it never creates or changes one.
- **One translation file.** An agent's instructions fix the input it accepts and the shape of its answer. `Infrastructure/Ai/FoundryAgentFormat.cs` is the only place that knows those shapes: it turns the request into the agent's input and the agent's answer into text. When an agent's owner changes its instructions, that is the one file to change. Until it is, that use answers 503.

As the agents stood on 19 November 2026:

| Agent | What it is sent | What it answers | State |
|---|---|---|---|
| `role-briefing` (version 6) | `{ "task": <the prompt file's instructions>, "data": <the data> }`. The agent follows the task over the data, so the prompt file's wording applies | One plain paragraph, naming cases as `{{C-1}}` | Working |
| `impact-summary` (version 5) | `<input_json>` built from the data: `period`, `filters`, `cohort_size`, `synthetic`, `measures[]`, `delay_reasons[]`, `supplementary[]`. A withheld value goes as `suppressed` with no number. The agent follows its own instructions; the prompt file's are not sent | JSON: paragraphs of sentences and a limits note, read into plain text | Working |
| `patient-notification` (version 5) | `<input_json>` naming one of the agent's own templates and the facts to fill it with. The agent adds no wording of its own; the prompt file's instructions are not sent | JSON with the draft (`draft_text`), read into plain text | Working |

**The patient message on Foundry.** The agent holds a registry of approved templates and fills the one CareLoop names. `FoundryAgentFormat` chooses it from the `situation` in the data:

| `situation` | Template | Condition fact sent |
|---|---|---|
| `appointment reminder` (an appointment is booked and still to come) | `T02` Appointment reminder | `appointment_status` = `confirmed`, with the appointment's date and time |
| `could not reach the patient` (the case is flagged unreachable) | `T06` Unsuccessful contact follow-up | `contact_attempt_status` = `unsuccessful` |
| `please contact the care team` (everything else) | `T05` Request to contact the care team | `callback_task_verified` = `true` |

Only these three administrative templates are ever named. The result-bearing ones (`T09` to `T12`) are never used.

The facts sent, and three things to know about them:

- `patient_name` is the placeholder `{first name}`. The name is never sent.
- `facility_contact` is the placeholder `{clinic contact}`. CareLoop holds no contact number for a facility, so the reviewer fills it in.
- `recipient_verified` and `channel_permitted` are sent as `true`, because the agent refuses without them. CareLoop does not itself verify a recipient: the draft goes to a nurse or clinician, who checks the recipient and the channel before anything is sent. **This is a proposal to confirm** with the agent's owner and the clinical lead.

On Foundry the draft is the template's fixed wording, so it has no reply instructions ("Reply 1 …") and, for email, no subject line. With a provider that takes the prompt, `patient-message.md` governs the wording instead.

`Ai:Foundry:SyntheticData` (default `true`) is what the impact agent is told when it asks whether the figures are synthetic. The samples in `src/ai_agents/` are not used by the API.

## Failure

| Situation | Answer |
|---|---|
| `Ai:Provider` is `NoOp` | 409 "AI is switched off"; nothing was read or sent |
| The provider cannot be reached, answers an error or 429, refuses, writes nothing, or does not answer within `Ai:TimeoutSeconds` | 503 "AI could not write this" |

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
| `Ai:Foundry:Agents` | `role-briefing`, `impact-summary`, `patient-notification` | Which agent writes each text, by name: the key is the use (`role-briefing`, `impact-summary`, `patient-message`) |
| `Ai:Foundry:SyntheticData` | `true` | Told to an agent that asks whether the figures are synthetic |

- **Docker Compose** passes `AI_PROVIDER`, `AZURE_AI_PROJECT_ENDPOINT`, `AI_TIMEOUT_SECONDS` and `AI_CACHE_HOURS`; see `.env.example`. A container has no `az login`, so a live check uses `dotnet run`.
- **Azure** (`infra/azure`): `API_AI_PROVIDER` sets `Ai__Provider`; `AZURE_AI_PROJECT_ENDPOINT` sets the project endpoint; with `AZURE_AI_FOUNDRY_ACCOUNT_ID` the deployment gives the API's identity Foundry User on that account.

### Trying it against the live agents

Run the API (it needs its database):

```bash
az login
Ai__Provider=Foundry Ai__Foundry__ProjectEndpoint=https://<account>.services.ai.azure.com/api/projects/<project> dotnet run --project src/api/CareLoop.Api
```

Or, without a database, run the opt-in test. It runs the three real endpoints with synthetic cases held in memory and does nothing unless the endpoint is set:

```bash
CARELOOP_LIVE_FOUNDRY_ENDPOINT=https://<account>.services.ai.azure.com/api/projects/<project> dotnet test tests/api/CareLoop.Tests --filter "FullyQualifiedName~Live_"
```

Run on 19 November 2026: all three uses answered 200 from their own agents, the repeated briefing was reused, the briefing named the seeded cases by their case numbers, and the patient drafts kept the `{first name}` and `{clinic contact}` placeholders.

## What is built

| Piece | State |
|---|---|
| `IAiClient`, `FoundryAiClient`, `NoOpAiClient` | Built |
| `FoundryAgentFormat` for `role-briefing`, `impact-summary` and `patient-notification` | Built, checked against the live agents |
| The three endpoints, `AiText` (prompt, reuse, 409 and 503) | Built |
| Prompt files and `AiPrompts` | Built |
| Migration `RemoveHandoffSummaries` (drops the `HandoffSummaries` table) | Built |
| Tests | `tests/api/CareLoop.Tests/Api/Ai/` |
| Dashboard display of the three texts | Not built yet (WS6). The handoff summary screen is removed from `src/web` |

## Before real data

Synthetic only today. Before any non-synthetic data reaches a model, the organization must confirm the region and data residency (all patient data and AI processing stay in the UAE), retention and logging settings, ADHICS controls, access roles, the network path, Prompt Shields on the models the agents use, and the applicable DoH and legal approvals. The same applies before pointing `Ai:Provider` at any other provider. `Ai:Foundry:SyntheticData` stays `true`, and `impact-summary.md` ends its summary with "Based on synthetic demonstration data.", until then.

## Open

- Whether staff-only AI text may be shown with a label and no approval step. It is today; record the decision.
- Sending an AI-drafted message to a patient. Today the draft is a preview and patients receive approved templates. Sending one would need a person's approval of the exact text, recorded as an event.
- Which roles may ask for a patient message draft. Today a nurse navigator or a clinician.
- The small-number threshold.
- The model's text is not checked against the data. If a wrong summary is ever seen, add a check for that case rather than a general parser.
- The patient message on Foundry: confirm the template chosen for each situation, who supplies a clinic contact number, and whether CareLoop may state that the recipient is verified and the channel permitted (see [The Foundry agents](#the-foundry-agents)).
- The `patient-notification` agent has a web search tool attached. An agent that is sent patient-related facts should not have a way to send them elsewhere; raise it with the agent's owner.
- The agents are called at their latest version, so a change to an agent's instructions can break a use until `FoundryAgentFormat` is updated. Agree how changes are announced.
- `src/ai_agents/` holds sample scripts the API does not use; decide whether to keep them.
