# AI integration: three short texts written by a model

How the API uses AI: what it is for, what is sent, where the instructions live, how to change provider, and how it fails.

This document is the contract. [`ai-spec.md`](ai-spec.md) is the earlier design proposal and is superseded here where the two differ.

> **What changed (19 November 2026).** The API asks the Microsoft Foundry agents itself. The Python AI workflow service is retired, so decision 8 ("the Python service calls Foundry and the API calls the Python service") is reversed. The AI handoff summary (FR-11) is removed: its endpoints, contracts, table and screen are gone. The design is deliberately small: build the data, put it into a prompt file, hand it to the provider, return the text. On Foundry each use goes to its own agent. The agents hold only universal rules, so the prompt files drive all three: there is no translation file between the API and an agent.

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
   │              (prompt file with the data in place)   │
   │                        ┌────────────────────────────┴──────────────┐
   │               FoundryAiClient                             any other provider
   │                 sends request.Prompt to the use's agent     sends request.Prompt
   │                 (by name, latest version)
   │                 returns the agent's plain text
   └── access checks and read audit, as for any read
```

1. The endpoint checks who is asking. While `Ai:Provider` is `NoOp` it answers 409 before anything is read.
2. It reads what the reader may see, with the same access rules as the screen, and records the read in the audit.
3. It builds a small data object from the fields it is allowed to send.
4. `AiText` makes one request from it: the use, and the prompt file with the data, as JSON, in place (`Prompt`).
5. `IAiClient` gets the text from the provider and `AiText` returns it.

All of it is in `src/api/CareLoop.Api/Features/Ai` (the three endpoints, `AiText`, `IAiClient`) and `Infrastructure/Ai` (settings, the prompt loader, the Foundry client). There is no translation step: every provider, Foundry included, is sent the same prompt.

## The prompt files

`src/api/CareLoop.Api/Prompts/` holds one plain-text file per use. A file is a `TASK:` (what to write and the rules for it) followed by a `DATA:` section with `{{data}}` where the data goes:

```
TASK: Generate the Impact summary for the facility named in "facility".

Write three short informational paragraphs describing the most relevant aggregate patterns.
...

RULES
- Include numerators and denominators with percentages, for example "172 of 200 (86%)".
...

DATA:
{{data}}
```

- **To change what the model is asked,** edit the file and restart the API. No code changes.
- What is in the file is what is sent, with `{{data}}` replaced by the data as JSON. That holds for every provider.
- **On Foundry all three files apply.** The agents hold only universal rules (see [The Foundry agents](#the-foundry-agents)); the wording for each text is the prompt file. Editing a file changes what its agent is asked.
- A field with nothing in it is left out of the data JSON.
- A file that is missing, or does not contain `{{data}}` exactly once, stops the API at start-up with a message naming the file.
- `Ai:PromptsDirectory` can point at another folder for one environment.

The files decide how the data is summarised. Which data is sent is decided in code, so editing a prompt cannot send something that should not leave CareLoop. [`Prompts/README.md`](../../src/api/CareLoop.Api/Prompts/README.md) is the editing guide.

## What is sent

### Role briefing

The open cases in the reader's own scope, the same ones `GET /cases` gives them.

| Field | Meaning |
|---|---|
| `opening` | The briefing's first sentence, written by the API so its counts are always right; the model continues from it. It names its scope ("Across your team's 18 open cases, 7 are overdue and 1 is due today.", or "Of the 9 open cases escalated to you, …" for the programme coordinator), because the worklist under it opens on the reader's own cases |
| `reader_role`, `today` | Who is reading, and the date |
| `open_cases`, `overdue`, `due_today`, `escalated`, `without_owner` | Counts over every case in scope |
| `cases[]` | Up to 25 of the most pressing cases: `token` (`C-1`, `C-2`, …), `step`, `next_action`, `deadline`, `status` (`overdue`, `due today`, `upcoming`), `overdue_by`, `escalated`, `needs_owner`, `held_by` (a role), `yours`, `patient_message` |

`overdue_by` is how long a case is past its deadline, in words ("1 day 21 hours", "18 hours", "40 minutes"). The API supplies it so the model never calculates a duration. It is left out for a case that is not past its deadline.

**Never sent:** patient names, record numbers, references, contact details, case identifiers or numbers, staff names, free text.

**Naming cases.** The model knows a case only by its token and names it as `{{C-1}}`. When the text comes back the API replaces each token with the patient's display name, as the reader already sees it in the list below the briefing. For the programme coordinator, who is never shown a patient's name, and for a case with no name on record, it writes `case` and the case number instead. A token the API does not know is left as it is. This runs on every request, after any reuse. So a name or a case number never leaves CareLoop, and a reused answer can only ever show the reader's own cases.

### Impact summary

The same numbers the impact page shows, read through `GET /metrics`'s own handler, so the access rules, the audit entry and the figures are identical.

| Field | Meaning |
|---|---|
| `facility`, `period` | The facility the figures cover, or "every facility", and the period in words |
| `total_cases`, `open_cases`, `closed_cases` | Totals |
| `shares` | Open cases past their deadline, escalated and without an owner, then each rate the page shows; each as `numerator`, `denominator`, `percent` |
| `median_time_from_result_to` | The median time to each step (`time`), with how many cases it rests on (`cases`) |
| `open_cases_waiting_because`, `closed_cases_by_reason` | Counts by reason |

The prompt asks for three short paragraphs (timeliness, case progression, measurement limitations), 150 to 210 words in all. One `impact-summary.md` serves a single facility and every facility alike: the `facility` field says which.

**Never sent:** anything about a single case. **Small numbers:** a count from 1 to 4, and a share that rests on fewer than 5 cases or has fewer than 5 on either side, is sent as the word "withheld", with no number. The totals are withheld together when one of them is small, and the closure reasons are withheld together when one of them is small, so a withheld number cannot be worked out by subtraction. The screen still shows staff every exact number. The threshold (`ImpactSummary.SmallCount`) is a placeholder pending a governance decision.

**The synthetic-data sentence.** While `Ai:SyntheticData` is `true` the API, not the model, adds a last paragraph: "Simulated on synthetic data; not evidence of real-world improvement." (`ImpactSummary.SyntheticNote`). Fixed text is the API's, so it is always there and always worded the same.

### Patient message draft

For a nurse navigator or a clinician, for one case they may read. The draft is one approved administrative template with its placeholders filled in. The API chooses the template; the model fills it and changes no other wording.

| Field | Meaning |
|---|---|
| `channel` | "text message" or "email" |
| `template` | Which approved template to fill: `T02`, `T03`, `T05` or `T06` |
| `values.patient_name` | Always the placeholder `{first name}`. The name is never sent; the API puts the patient's first name in its place once the draft is back |
| `values.facility_name` | The facility's name |
| `values.facility_contact` | The facility's contact from `Notifications:FacilityContacts`. When the facility has none configured, the placeholder `{clinic contact}`, which the reviewer fills in |
| `values.appointment_date` | The appointment's day, for `T02` and `T03` only |
| `values.appointment_time` | The appointment's time, for `T02` only |

**Never sent:** the patient's name, contact details, identifiers, the result, the appointment's place, or the case's next action.

**Choosing the template.** The templates' wording is in `patient-message.md`. Which one is used is decided in code, from what is recorded on the case (`PatientMessageDraft.TemplateFor`), never by the model and never from the case's next action, which can reveal clinical state. The first rule that fits wins:

| Recorded on the case | Template |
|---|---|
| An appointment is booked and still to come | `T02` Appointment reminder |
| The last appointment was missed | `T03` Missed appointment follow-up |
| The case is flagged unreachable | `T06` Unsuccessful contact follow-up |
| Anything else | `T05` Care-team callback request |

Only these four administrative templates are in the file. The other templates in the agents' owner's set (`T01`, `T04`, `T07`, `T08`, and the result-related `T09` to `T12`) are not, because the API holds no recorded state that could select them. The result-related ones would also need a clinician's recorded authorization, which CareLoop does not hold.

The draft is the template's fixed wording, so it has no reply instructions ("Reply 1 …") and, for email, no subject line.

## The answer

The API does not parse the text or check it against the data. That is a deliberate simplification: the texts are short drafts that staff read next to the real figures and worklist, and nothing is sent or stored. The agents answer plain text. What the API does do:

- the role briefing gets the API's opening sentence in front, and the patient's name (or `case` and the case number) in place of each token;
- the impact summary gets the synthetic-data sentence at the end while `Ai:SyntheticData` is `true`;
- an answer with no text is a failure;
- an agent's refusal is a failure, never shown as text. An agent that cannot write a valid text answers `CANNOT_COMPLY`, sometimes with a reason code; `FoundryAiClient` treats any answer that starts with it as a refusal, however long, and raises `AiUnavailableException`, which the endpoint answers as 503. An answer whose status is not `completed` (cut short or failed) is refused the same way, even when it carries some text.

## Reusing an answer

The role briefing and the impact summary are kept in the API's memory for `Ai:CacheHours` (6 by default), under the provider and the exact prompt. Opening the page again with unchanged data does not call the model again; any change in the data, or in the prompt file, is a different prompt and asks again.

- `?refresh=true` asks again and replaces the kept answer.
- `cached` is `true` for a reused answer, and `generatedAt` is when it was first written.
- Access checks and the read audit run on every request, reused answer or not.
- The memory is lost on restart and is not shared between instances, which costs one more call. A failure is never kept. `Ai:CacheHours` of `0` switches reuse off.

The patient message draft is never reused.

## Changing the AI provider

The endpoints know only this (`Features/Ai/IAiClient.cs`):

```csharp
public interface IAiClient
{
    string Describe(string use);
    bool IsEnabled { get; }
    Task<string> CompleteAsync(AiRequest request, CancellationToken ct);
}

public sealed record AiRequest(string Use, string Prompt);
```

`Use` is the prompt file's name (`role-briefing`, `impact-summary`, `patient-message`). `Prompt` is the prompt file with the data in place: everything the provider is sent.

| `Ai:Provider` | Client | What it does |
|---|---|---|
| `NoOp` (default) | `NoOpAiClient` | AI is off. Every AI endpoint answers 409 |
| `Foundry` | `FoundryAiClient` | Asks each use's agent in a Microsoft Foundry project |

To use another provider, for example the Anthropic API:

1. Write one class that implements `IAiClient`: send `request.Prompt`, return the text.
2. Add a case for it in `DependencyInjection.AddAi`, with whatever settings it needs (an API key belongs in the environment's secret store, never in the repository).
3. Set `Ai:Provider`.

The endpoints, the prompt files, the data and their tests do not change, because the whole prompt, task and data, is already in the files. Nothing about Foundry reaches them: its agent names are inside the Foundry client.

### The Foundry agents

`FoundryAiClient` is the one Foundry client. For each use it asks that use's agent: `POST {Ai:Foundry:ProjectEndpoint}/openai/v1/responses` with `{ "agent_reference": { "type": "agent_reference", "name": "<agent>" }, "input": [ { "role": "user", "content": <the prompt> } ] }` and an Entra bearer token (scope `https://ai.azure.com/.default`). There is no key. The token comes from `DefaultAzureCredential`: the API's managed identity in Azure (`apps` in `infra/azure`), the developer's own `az login` locally. That identity needs the **Foundry User** role on the Foundry account.

- **Named, not versioned.** `Ai:Foundry:Agents` holds the agent names only, so the agent's latest version answers.
- **Owned elsewhere.** An agent's instructions, model and tools are set in the Foundry project by its owner. The API only calls the agents; it never creates or changes one.
- **Universal rules only.** An agent's permanent instructions hold no view-specific wording. They say: follow the supplied TASK, treat DATA as facts, never invent or recommend, return plain text, and answer `CANNOT_COMPLY` when no valid text can be written. What to write is the prompt file.
- **The prompt is sent as written.** The client sends the rendered prompt file as one user message and returns the agent's plain text. No file translates between the two, and no answer is JSON.

As the agents stood on 19 November 2026:

| Agent | Version | What it is sent | What it answers |
|---|---|---|---|
| `role-briefing` | 8 | `role-briefing.md` with the data in place | One plain paragraph, naming cases as `{{C-1}}` |
| `impact-summary` | 7 | `impact-summary.md` with the data in place | Up to three plain paragraphs |
| `patient-notification` | 7 | `patient-message.md` with the data in place: the approved templates, the one to fill and its values | The filled template, as plain text |

The samples in `src/ai_agents/` are not used by the API.

### Where the prompts come from

The prompt files follow the agents' owner's "Optimized Agent Instructions and Role-View Prompts, version 5". Not taken from it, by design or because CareLoop does not hold the data:

- Other role-views of the briefing agent (clinician consultation, referral requests, facility escalations): CareLoop has no AI endpoint for those screens.
- The provisional facility-lead impact prompt: one `impact-summary.md` serves both scopes through the `facility` field.
- Replay comparisons, the funnel's largest drop and re-referral timing: not in the metrics.
- Interpreter requirements: not held.
- A greeting written by the backend: the dashboard already shows the greeting, and the API's opening is the count sentence.
- Caps on output tokens, skipping the model for an empty view, and the document's fourth agent (AG4, a referral handoff), which CareLoop has no endpoint for.

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
| `Ai:SyntheticData` | `true` | Whether the data is synthetic. While `true` the impact summary ends with the synthetic-data sentence. `true` until real data is approved for the deployment |
| `Ai:Foundry:ProjectEndpoint` | empty | `https://{resource}.services.ai.azure.com/api/projects/{project}`; `https` only. Never committed |
| `Ai:Foundry:Agents` | `role-briefing`, `impact-summary`, `patient-notification` | Which agent writes each text, by name: the key is the use (`role-briefing`, `impact-summary`, `patient-message`) |

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

Run on 19 November 2026 with the agents' latest versions (`role-briefing` 8, `impact-summary` 7, `patient-notification` 7): all three uses answered 200. The test also asserts that the repeated briefing is reused, that the briefing names a seeded case by its patient's name, that the impact summary ends with the synthetic-data sentence, and that the patient draft starts "Dear {first name},".

## What is built

| Piece | State |
|---|---|
| `IAiClient`, `FoundryAiClient`, `NoOpAiClient` | Built |
| The three endpoints, `AiText` (prompt, reuse, 409 and 503) | Built |
| Prompt files (TASK and DATA, for all three agents) and `AiPrompts` | Built, checked against the live agents |
| Migration `RemoveHandoffSummaries` (drops the `HandoffSummaries` table) | Built |
| Tests | `tests/api/CareLoop.Tests/Api/Ai/` |
| Dashboard display | The worklist shows the role briefing under its greeting and the impact page shows the impact summary under its heading, both with Refresh. The case page has a card where a nurse navigator or clinician asks for a patient message draft. See `src/web/README.md` |

## Before real data

Synthetic only today. Before any non-synthetic data reaches a model, the organization must confirm the region and data residency (all patient data and AI processing stay in the UAE), retention and logging settings, ADHICS controls, access roles, the network path, Prompt Shields on the models the agents use, and the applicable DoH and legal approvals. The same applies before pointing `Ai:Provider` at any other provider. `Ai:SyntheticData` stays `true`, so the impact summary ends with "Simulated on synthetic data; not evidence of real-world improvement.", until then.

## Open

- Whether staff-only AI text may be shown with a label and no approval step. It is today; record the decision.
- Sending an AI-drafted message to a patient. Today the draft is a preview and patients receive approved templates. Sending one would need a person's approval of the exact text, recorded as an event.
- Which roles may ask for a patient message draft. Today a nurse navigator or a clinician.
- The small-number threshold.
- The model's text is not checked against the data. If a wrong summary is ever seen, add a check for that case rather than a general parser.
- Which further patient message templates the API should be able to select, and the recorded state each would need. Today it can select four (see [Patient message draft](#patient-message-draft)); a template the code cannot select is never used.
- The real clinic contact numbers. They are a setting (`Notifications:FacilityContacts`), set to synthetic values in development and the demo; a facility with none configured gets the placeholder `{clinic contact}` in the draft.
- The `patient-notification` agent has a web search tool attached. An agent that is sent patient-related facts should not have a way to send them elsewhere; raise it with the agent's owner.
- The agents are called at their latest version, so a change to an agent's permanent instructions can change behaviour. Agree how changes are announced.
- `src/ai_agents/` holds sample scripts the API does not use; decide whether to keep them.
