# Prompts

One plain-text file per AI use. Each file is a `TASK:` (what to write and the rules for it) followed by a `DATA:` section with `{{data}}`, where the API puts the data.

| File | Used for | Endpoint |
|---|---|---|
| `role-briefing.md` | Home page: what the signed-in role has in front of them | `GET /ai/role-briefing` |
| `impact-summary.md` | Impact page: a reading of the figures shown | `GET /ai/impact-summary` |
| `patient-message.md` | Case page: a draft message to the patient, for staff to read | `GET /cases/{caseId}/patient-message-draft` |

## The shape of a file

```
TASK: <what to write>

<the rules: content, style, length>

DATA:
{{data}}
```

The provider is sent the file as written, with `{{data}}` replaced by the data as JSON. The model follows the TASK and treats the DATA as facts.

## Changing a prompt

1. Edit the file. Keep the `TASK:` opening and the `DATA:` section.
2. Keep `{{data}}` in it exactly once.
3. Restart the API. Files are read at start-up.

A file that is missing, or does not contain `{{data}}` exactly once, stops the API at start-up with a message naming the file.

## What the data is

The API decides what data is sent; the file decides what is written from it. Field names in the data are in `snake_case`, and the task can refer to them. A field with nothing in it is left out.

- **Role briefing:** the opening sentence (`opening`, written by the API), the reader's role, today's date, counts (open, overdue, due today, escalated, without owner), and up to 25 of the most pressing cases. Each case has a token (`C-1`, `C-2`, …), its step, next action, deadline, status and, when it is past its deadline, `overdue_by`: how long, in words, supplied so the model never calculates. No names, numbers or references are sent. The model names a case as `{{C-1}}`; after the text comes back the API puts the patient's name there, as the reader already sees it in the list. For the programme coordinator, or a case with no name, it writes `case` and the case number.
- **Impact summary:** the totals, rates, median times and reasons the Impact page shows. A count from 1 to 4 is replaced by the word "withheld". The file asks for three short paragraphs: timeliness, case progression and measurement limitations. Do not put the synthetic-data sentence in the file: the API adds "Simulated on synthetic data; not evidence of real-world improvement." itself while `Ai:SyntheticData` is `true`.
- **Patient message:** the channel, the template to fill (`template`) and its `values`: `patient_name` (always the placeholder `{first name}`), `facility_name`, `facility_contact` (always the placeholder `{clinic contact}`; CareLoop holds no clinic contact number) and, when relevant, `appointment_date` and `appointment_time`. Never the patient's name, contact details, result or the case's next action.

Which data is sent is in code (`Features/Ai`), so editing a prompt cannot send something that should not leave CareLoop.

## The patient message templates

`patient-message.md` holds the approved administrative templates. The model fills the one named in `template` with `values` and changes no other wording.

The API chooses the template in code, from what is recorded on the case (`PatientMessageDraft.TemplateFor` in `Features/Ai/PatientMessageDraft.cs`). The first rule that fits wins:

| Recorded on the case | Template |
|---|---|
| An appointment is booked and still to come | `T02` Appointment reminder |
| The last appointment was missed | `T03` Missed appointment follow-up |
| The case is flagged unreachable | `T06` Unsuccessful contact follow-up |
| Anything else | `T05` Care-team callback request |

To add a template, do both:

1. Add the template's text to `patient-message.md`, under its identifier, with placeholders in single braces (`{facility_name}`).
2. Add a rule to `PatientMessageDraft.TemplateFor` that selects it from recorded state, and supply any new value in `BuildData`.

A template the code cannot select is never used. To change a template's wording, edit it in the file; that is the wording the draft will have. Templates that mention a result are not in the file: the reporting physician gives results.

## On Microsoft Foundry

With `Ai:Provider` set to `Foundry`, each use goes to its own agent in the Foundry project. The agents hold only universal rules there (follow the TASK, treat DATA as facts, never invent or recommend, return plain text, answer `CANNOT_COMPLY` when no valid text can be written). The wording for each text is the file:

| File | Agent | Does this file's wording apply? |
|---|---|---|
| `role-briefing.md` | `role-briefing` | Yes |
| `impact-summary.md` | `impact-summary` | Yes |
| `patient-message.md` | `patient-notification` | Yes |

So editing a file changes what its agent is asked. The agent's owner changes only the universal rules. Any other provider is sent the same prompt. [`docs/ai-design/integration.md`](../../../../docs/ai-design/integration.md) has the details.
