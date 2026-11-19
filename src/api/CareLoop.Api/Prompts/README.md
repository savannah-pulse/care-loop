# Prompts

One plain-text file per AI use. Each file is the instructions for the summary, with `{{data}}` where the API puts the data to summarise.

| File | Used for | Endpoint |
|---|---|---|
| `role-briefing.md` | Home page: what the signed-in role has in front of them | `GET /ai/role-briefing` |
| `impact-summary.md` | Impact page: a reading of the figures shown | `GET /ai/impact-summary` |
| `patient-message.md` | Case page: a draft message to the patient, for staff to read | `GET /cases/{caseId}/patient-message-draft` |

## Changing a prompt

1. Edit the file. Write the instructions however you like.
2. Keep `{{data}}` in it exactly once.
3. Restart the API. Files are read at start-up.

For a provider that takes a prompt, what you see in the file is what is sent, with `{{data}}` replaced by the data as JSON. A file that is missing, or has no `{{data}}`, stops the API at start-up with a message naming the file.

## What the data is

The API decides what data is sent; the file decides how it is summarised. Field names in the data are in `snake_case`, and the instructions can refer to them.

- **Role briefing:** the opening sentence (written by the API), the reader's role, today's date, counts (open, overdue, due today, escalated, without owner), and up to 25 of the most pressing cases with a token (`C-1`, `C-2`, …), their step, next action, deadline and status. No names, numbers or references: the API swaps each token for the case number after the text comes back.
- **Impact summary:** the totals, rates, median times and reasons the Impact page shows. A count from 1 to 4 is replaced by the word "withheld".
- **Patient message:** the channel, its length limit, the clinic's name, which kind of administrative message fits, what the message is for (a sentence with no result in it) and an upcoming appointment's day and time. Never the patient's name, contact details, result or the case's next action.

Which data is sent is in code (`Features/Ai`), so editing a prompt cannot send something that should not leave CareLoop.

## On Microsoft Foundry

With `Ai:Provider` set to `Foundry`, each use goes to its own agent in the Foundry project, and an agent has instructions of its own there:

| File | Agent | Does this file's wording apply? |
|---|---|---|
| `role-briefing.md` | `role-briefing` | Yes. The agent is given these instructions as its task |
| `impact-summary.md` | `impact-summary` | Yes. The agent is given these instructions as its task |
| `patient-message.md` | `patient-notification` | No. The agent fills an approved template CareLoop sends it, with the values it is sent |

To change wording that does not come from these files, the agent's owner changes the agent. With any other provider, all three files apply as written. [`docs/ai-design/integration.md`](../../../../docs/ai-design/integration.md) has the details.
