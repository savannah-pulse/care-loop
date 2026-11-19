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

What you see in the file is what is sent to the model, with `{{data}}` replaced by the data as JSON. A file that is missing, or has no `{{data}}`, stops the API at start-up with a message naming the file.

## What the data is

The API decides what data is sent; the file decides how it is summarised. Field names in the data are in `snake_case`, and the instructions can refer to them.

- **Role briefing:** the reader's role, today's date, counts (open, overdue, due today, escalated, without owner), and up to 25 of the most pressing cases with their step, next action, deadline and status. No names, numbers or references.
- **Impact summary:** the totals, rates, median times and reasons the Impact page shows. A count from 1 to 4 is replaced by "withheld: fewer than 5".
- **Patient message:** the channel, its length limit, the clinic's name, what the message is for (a sentence with no result in it) and an upcoming appointment's day and time. Never the patient's name, contact details, result or the case's next action.

Which data is sent is in code (`Features/Ai`), so editing a prompt cannot send something that should not leave CareLoop.

## The model

The model is not set here. `Ai:Provider` chooses the provider and, for Foundry, `Ai:Foundry:Model` names the model. The same files work with any provider.
